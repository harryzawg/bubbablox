// this could still be split into separate files for better organization (but i don't care)
// i removed all the game server stuff, and just turned it into exclusively a renderer
// everything is fixed (i removed texture generation because it would freeze the whole ws server? EDIT: fixed, but it's too late now so i don't really care it just uses the content url onsite now)
import StdExceptions from '../helpers/Exceptions';
import fs = require('fs');
import path = require('path');
import util = require('util');
import cp = require('child_process');
import pf = require('portfinder');
const sleep = util.promisify(setTimeout);
import os = require('os');
import conf from '../helpers/Config';
import * as models from '../models';
import sharp = require('sharp');
import uuid = require('uuid');
import http = require('http');
import axios from 'axios';
import { IncomingWebhook } from '@slack/webhook';
const rccPort = conf.rccPort || 64989; // default: 64989

const sendtohook = async (message: string) => {
    if (!conf.webhook) {
        console.log('webhook not found, not sending');
        return;
    }

    try {
        await axios.post(conf.webhook, {
            content: message,
        }, {
            headers: {
                'Content-Type': 'application/json'
            }
        });
        console.log('sent to webhook:', message);
    } catch (e) {
        console.error('failed to send to webhook:', e.response?.data || e.message);
    }
};

const isPortAvailable = (port: number): Promise<boolean> => {
    return new Promise((res, rej) => {
        let didAnything = false;
        let timer = setTimeout(() => {
            if (didAnything) return;
            
            console.log('[info] isPortAvailable timeout for', port);
            res(false);
            try {
                server.close();
            } catch(e) {}
            // @ts-ignore
            timer = undefined;
        }, 5 * 1000);
        const server = http.createServer();
        server.on('error', () => {
            if (didAnything) return;
            if (timer) {
                clearTimeout(timer);
            }
            didAnything = true;
            res(false);
        });
        server.on('listening', () => {
            server.close();
            if (didAnything) return;
            console.log('[info] is port available:', port);
            sendtohook(`port ${port} is available, using that`);
            if (timer) {
                clearTimeout(timer);
            }
            didAnything = true;
            sleep(1000).then(() => {
                res(true);
            })
        })
        server.listen(port);
    });
}

const getFreeRccPort = async (): Promise<number> => {
    for (let i = 6001; i < 50000; i++) {
        let avail = await isPortAvailable(i);
        console.log('[info] port available?', i, avail);
        if (avail) {
            return i;
        }
    }
    throw new Error('No ports available');
}

import getScripts from '../scripts';
const scripts = getScripts();

import axiosStatic from 'axios';
import { awaitResult, doesCallbackExist, getResult, resolutionMultiplier, getUploadCallbacks } from '../rendering';
const axiosClient = axiosStatic.create({
    headers: {
        'user-agent': 'GameServer/1.0',
    }
});

const maxRendersBeforeRestart = 130;

interface IQueueEntry {
    request: string;
    jobId: string;
    createdAt: number;
}

interface IRccConnection {
    close: () => void;
    port: number;
    id: string;
}

interface IRenderEntry {
    rccReference: IRccConnection | null;
    rccClosed: boolean;
    renderCount: number;
    runningGames: number;
    serverId: string;
}

const maxJobQueueRunningCount = Math.max(1, Math.trunc(os.cpus().length / 2));
console.log('[info] system thread count', maxJobQueueRunningCount);

const truncateForDiscordMessage = (msg: string) => {
    msg = msg.replace(/\*/g, '\\*').replace(/\_/g, '\\_').replace(/\~/g, '\\~').replace(/\`/g, '\\`');
    const maxLen = 500;
    if (msg.length < maxLen)
        return msg;
    return msg.substring(msg.length - maxLen) + '...';
}

/**
 * handler for all rendering reqs
 */
export default class CommandHandler extends StdExceptions {
    private reservedPorts: number[] = [];
    private RenderRcc: IRenderEntry[] = [];
    private JobQueue: IQueueEntry[] = [];
    private RunningJobIds: string[] = [];
    private JobQueueRunningCount = 0;
    
    constructor() {
        super();
        this.onStartup();
    }

    private randomId(): string {
        return uuid.v4();
    }

    private onStartup() {
		// i don't think this is used
    }

	// ik a weird way to do it but it works the best in my experience
	private makerccbat(port: number): string {
		const rccexe = conf.rccexe || 'RCCService.exe';
		const rccPath = conf.rcc;
		if (!rccPath) {
			throw new Error('RCC path is not configured');
		}
		const content = `@echo off
		START ${rccexe} "${path.join(rccPath, rccexe)}" -console -verbose -port ${port}
		`;
		const batpath = path.join(__dirname, `../../rcc-${port}.bat`);
		fs.writeFileSync(batpath, content);
		return batpath;
	}

	// also janky asf but it wasn't working at all
    private async startRcc(render: IRenderEntry): Promise<void> {
        if (render.rccReference || (await this.isRccReady(rccPort))) {
            console.log('[info] rcc already running');
            if (!render.rccReference) {
                render.rccReference = {
                    close: () => {},
                    port: rccPort,
                    id: this.randomId(),
                }
            }
            return;
        }

        console.log('[info] looking for port...');
        let start = Date.now();
        let portToRunOn = await getFreeRccPort();
        let lastRecPort = portToRunOn;
        
        while (this.reservedPorts.includes(portToRunOn)) {
            console.log('[info] port is already in use', portToRunOn);
            await sleep(1000);
            portToRunOn = await getFreeRccPort();
            let cur = Date.now();
            if (lastRecPort !== portToRunOn) {
                start = Date.now();
                lastRecPort = portToRunOn;
            }
            let diff = cur - start;
            if (diff > 4 * 1000) {
                console.log('[info] port has been reserved for over 4s despite being available, will use it');
                await sendtohook('got reserved RCC port, so using that');
                break;
            }
        }
        
        this.reservedPorts.push(portToRunOn);
        console.log('[info] found port for rcc:', portToRunOn);
        await sendtohook(`got RCC port: ${portToRunOn}, will start shortly`);

        try {
            await axiosClient.request({
                method: 'GET',
                url: `http://127.0.0.1:${portToRunOn}/`,
                headers: {
                    'Content-Type': 'text/xml; charset=utf-8',
                },
                validateStatus: () => true,
                timeout: 1000,
            });
            
            render.rccReference = {
                close: () => {},
                port: portToRunOn,
                id: this.randomId(),
            };
            return;
        } catch (e) {
            console.log('[info] no existing RCC found, starting new one');
        }

		const rccexe = conf.rccexe || 'RCCService.exe';
		const rccPath = conf.rcc;
		if (!rccPath) {
			throw new Error('RCC path is not configured');
		}

		// this doesn't print any RCC stderr or stdout cause it likes to print all the http codes while trying to upload logs
		const rcc = cp.spawn(path.join(rccPath, rccexe), [
			'-console',
			'-verbose',
			'-port',
			portToRunOn.toString()
		], {
			cwd: rccPath,
			detached: true,
			stdio: 'ignore',
			windowsHide: true
		});
        
        rcc.unref();
        
        render.rccClosed = false;
        render.rccReference = {
            id: this.randomId(),
            port: portToRunOn,
            close: () => {
                try {
                    rcc.kill('SIGINT');
                    render.rccReference = null;
                } catch (e) { }
            }
        };

        console.log('[info] waiting for rcc...');
        try {
            await this.waitForRcc(render, portToRunOn);
        } catch(e) {
            if (render.rccReference) {
                render.rccReference.close();
            }
            render.rccReference = null;
            throw e;
        }
        console.log('[info] RCC ok');
    }

    protected async isRccReady(port: number) {
        try {
            const result = await axiosClient.request({
                method: 'GET',
                url: 'http://127.0.0.1:' + port + '/',
                headers: {
                    'Content-Type': 'text/xml; charset=utf-8',
                },
                validateStatus: () => true,
                timeout: 1000,
            });
            await sleep(1500);
            return true;
        } catch (e) {
            return false;
        }
    }

    protected async waitForRcc(render: IRenderEntry, port: number) {
        let start = Date.now();
        do {
            let elapsedSeconds = (Date.now() - start) / 1000;
            if (elapsedSeconds > 5) {
                break;
            }
            if (render.rccClosed) {
                throw new Error('RCC was closed');
            }
            try {
                const result = await axiosClient.request({
                    method: 'GET',
                    url: '127.0.0.1' + port + '/',
                    headers: {
                        'Content-Type': 'text/xml; charset=utf-8',
                    },
                    validateStatus: () => true,
                    timeout: 1000,
                });
                await sleep(150);
                console.log('[info] rcc ok');
                return
            } catch (e) {
                console.log('[info] rcc not ok', e.message);
                await sleep(250);
            }
        } while (true)
    }
	
	// ALWAYS set expiration to 60. when it's like 12 hours all the jobs fill up, and it can't allocate anything
	// (this was the cause of RCC crashing a lot)
    private createSoapRequest(script: string, jobId: string): { request: string; jobId: string; createdAt: number; } {
        const scriptToSend = script.replace(/InsertJobIdHere/g, jobId);
        const xml = `<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <OpenJobEx xmlns="http://roblox.com/">
        <job>
            <id>${jobId}</id>
            <category>0</category>
            <cores>1</cores>
            <expirationInSeconds>60</expirationInSeconds>
        </job>
        <script>
            <name>GameStart</name>
            <script>
            <![CDATA[
            ${scriptToSend}
            ]]>
            </script>
        </script>
    </OpenJobEx>
  </soap:Body>
</soap:Envelope>`;
        return {
            request: xml,
            jobId,
            createdAt: Date.now(),
        }
    }

    public addToQueue(item: IQueueEntry): void {
        this.JobQueue.push(item);
        console.log('[info] adding item to queue, queue length =', this.JobQueue.length, 'rcc runner count =', this.JobQueueRunningCount, 'rcc instance count =', this.RenderRcc.length);
        if (this.JobQueueRunningCount < maxJobQueueRunningCount) {
            console.log('[info] start job queue because it is not running');
            this.runJobQueue();
        }
    }

    private async GetRccForRender(): Promise<IRenderEntry> {
        let applicable = this.RenderRcc.filter(v => !v.rccClosed && v.rccReference).sort((a, b) => {
            return a.runningGames > b.runningGames ? 1 : a.runningGames === b.runningGames ? (
                a.renderCount > b.renderCount ? 1 : a.renderCount === b.renderCount ? 0 : -1
            ) : -1;
        });
        if (applicable && applicable.length) {
            let i = 0;
            for (const rcc of applicable) {
                console.log('[info] picked RCC instance data: running =', rcc.runningGames, 'renders =', rcc.renderCount, 'idx =', i);
                i++;

                if (rcc.renderCount >= maxRendersBeforeRestart) {
                    console.log('[info] RCC picked has too many renders, requesting a shutdown', rcc.renderCount, 'vs', maxRendersBeforeRestart);
                    await this.requestRccThumbnailerClose(rcc);
                } else {
                    return rcc;
                }
            }
        }
        console.log('[info] no applicable RCC instances for render, will create one');
        const id = this.randomId();

        const render = {
            rccClosed: false,
            rccReference: null,
            renderCount: 0,
            runningGames: 0,
            serverId: id,
        } as IRenderEntry;
        
        let start = Date.now();
        await this.startRcc(render);
        if (!render.rccReference) {
            console.error('undefined RCC reference');
            process.exit(1);
        }
        this.RenderRcc.push(render);
        console.log('[info] started a new RCC instance for renders. time =', (Date.now() - start), 'ms');
        return render;
    }

    public removeFromRunningJobs(jobId: string): void {
        this.RunningJobIds = this.RunningJobIds.filter(v => v !== jobId);
    }

	private async runJobQueueTask(rcc: IRenderEntry, job: IQueueEntry) {
	  if (!rcc?.rccReference) {
		throw new Error('RCC reference is not available - it was probably closed');
	  }
	  
	  const multiplier = 
		job.request.includes('imageTexture') ? resolutionMultiplier.texture :
		job.request.includes('meshThumbnail') ? resolutionMultiplier.mesh :
		resolutionMultiplier.asset;

	  // register callback BEFORE sending to RCC
	  const resultPromise = getResult(job.jobId, multiplier);

	  try {
		await axiosClient.request({
		  method: 'POST',
		  url: `http://127.0.0.1:${rcc.rccReference.port}/`,
		  headers: { 'Content-Type': 'text/xml; charset=utf-8' },
		  data: job.request,
		  timeout: 2 * 60 * 1000,
		});

		return await resultPromise;
	  } catch (error) {
		// clean up on failure
		const callbacks = getUploadCallbacks();
		if (callbacks[job.jobId]) {
		  delete callbacks[job.jobId];
		}
		throw error;
	  }
	}
    
    private async requestRccThumbnailerClose(rcc: IRenderEntry) {
        if (rcc.rccReference && !rcc.rccClosed) {
            rcc.rccReference.close();
        }
        this.RenderRcc = this.RenderRcc.filter(v => v !== rcc);
    }

    public async runJobQueue() {
        this.JobQueueRunningCount++;
        try {
            while (true) {
                let item = this.JobQueue[0];
                if (!item) {
                    console.log('[info] job queue empty, no more jobs left');
                    return;
                }
                
                if (this.RunningJobIds.includes(item.jobId)) {
                    console.log('[info] this job is already running. will skip. id =', item.jobId);
                    continue;
                }
                
                this.JobQueue = this.JobQueue.filter(v => v.jobId !== item.jobId);
                this.RunningJobIds.push(item.jobId);
                
                let rcc = await this.GetRccForRender();
                let msSinceCreation = Date.now() - item.createdAt;
                
                if (!doesCallbackExist(item.jobId)) {
                    if (msSinceCreation >= 60 * 1000) {
                        console.log('[warn] skipping job', item.jobId, 'because a callback for it does not exist and it was created over 1m ago');
                        this.RunningJobIds = this.RunningJobIds.filter(v => v !== item.jobId);
                        continue;
                    } else {
                        console.log('[info] doesCallbackExist returned false, but job was created', msSinceCreation, 'ms ago, so run it anyway');
                    }
                }
                
                console.log('[jq] run', item.jobId);
                rcc.runningGames++;
                
                try {
                    await this.runJobQueueTask(rcc, item);
                    rcc.renderCount++;
                } catch (e) {
                    if (e && e.isAxiosError && !e.response) {
                        await this.requestRccThumbnailerClose(rcc);
                    }
                    
                    if (this.RunningJobIds.includes(item.jobId)) {
                        this.JobQueue = [item, ...this.JobQueue];
                        this.RunningJobIds = this.RunningJobIds.filter(v => v !== item.jobId);
                    }
                    console.error('[error] [jq]', item.jobId, e);
                } finally {
                    rcc.runningGames--;
                }
                console.log('[info] [jq] task', item.jobId, 'finished');
            }
        } catch (e) {
            throw e;
        } finally {
            this.JobQueueRunningCount--;
        }
    }

    public async Cancel(jobId: string): Promise<null> {
        if (this.JobQueue.find(v => v.jobId === jobId)) {
            this.JobQueue = this.JobQueue.filter(v => v.jobId !== jobId);
        } else {
            this.removeFromRunningJobs(jobId);
        }
        return null;
    }

    public async GenerateThumbnailAsset(assetId: number): Promise<string> {
		const jobId = uuid.v4();
		const resultPromise = getResult(jobId, resolutionMultiplier.mesh);
        try {
            const job = this.createSoapRequest(
                scripts.assetThumbnail
                    .replace(/\{1234\}/g, `{${assetId}}`)
                    .replace(/_X_RES_/g, (420 * resolutionMultiplier.asset).toString())
                    .replace(/_Y_RES_/g, (420 * resolutionMultiplier.asset).toString()),
                jobId
            );
            
            await sendtohook(`generating asset ${assetId}'s thumbnail`);
            this.addToQueue(job);
            const result = await resultPromise;
            await sendtohook(`generated asset ${assetId}'s thumbnail`);
            return result.thumbnail;
        } catch (e) {
			this.removeFromRunningJobs(jobId);
            await sendtohook(`failed to generate thumbnail for ${assetId}: ${e.message}`);
            throw e;
        }
    }

    private async GetTeeShirtThumb(assetId: number): Promise<Buffer> {
        const result = await axiosClient.get(`${conf.baseUrl}/asset/?id=${assetId}`, {
            responseType: 'arraybuffer',
            headers: {
                'bot-auth': conf.websiteBotAuth,
            }
        });
        return Buffer.from(result.data, 'binary');
    }
    
    private bgBuffer?: Buffer = undefined;

    public async GenerateThumbnailTeeShirt(assetId: number, contentId: number): Promise<string> {
        if (!this.bgBuffer) {
            this.bgBuffer = fs.readFileSync(path.join(__dirname, '../../TeeShirtTemplate.png'));
            console.log('[info] read teeShirtBgBuffer into memory. size =', this.bgBuffer.length, 'bytes');
        }
        const bg = await sharp(this.bgBuffer);
        const content = await this.GetTeeShirtThumb(contentId);
        const image = await sharp(content).resize(250, 250, {
            fit: 'contain',
        }).png().toBuffer();
        bg.composite([
            {
                top: 85,
                left: 85,
                input: image,
            }
        ]);
        const buff = await bg.png().toBuffer();
        return buff.toString('base64');
    }

	public async GenerateThumbnailMesh(assetId: number): Promise<string> {
	  const jobId = uuid.v4();
	  const resultPromise = getResult(jobId, resolutionMultiplier.mesh);

	  try {
		const job = this.createSoapRequest(
		  scripts.meshThumbnail
			.replace(/\{1234\}/g, `{${assetId}}`)
			.replace(/_X_RES_/g, (420 * resolutionMultiplier.mesh).toString())
			.replace(/_Y_RES_/g, (420 * resolutionMultiplier.mesh).toString()),
		jobId
		);
		
		await sendtohook(`generating mesh's (${assetId}) thumbnail`);
		this.addToQueue(job);
		const result = await resultPromise;
		await sendtohook(`generated mesh's (${assetId}) thumbnail`);
		return result.thumbnail;
	  } catch (error) {
		this.removeFromRunningJobs(jobId);
		throw error;
		await sendtohook(`failed to generate mesh thumbnail for ${assetId}: ${error.message}`);
	  }
	}

    public async GenerateThumbnailHead(assetId: number): Promise<string> {
        const job = this.createSoapRequest(
            scripts.headThumbnail
                .replace(/\{1234\}/g, `{${assetId}}`)
                .replace(/_X_RES_/g, (420 * resolutionMultiplier.asset).toString())
                .replace(/_Y_RES_/g, (420 * resolutionMultiplier.asset).toString()),
            uuid.v4());
			await sendtohook(`generating head's (${assetId}) thumbnail`);
        this.addToQueue(job);
        return (await getResult(job.jobId, resolutionMultiplier.asset)).thumbnail;
		await sendtohook(`generated head's (${assetId}) thumbnail`);
    }

    public async GenerateThumbnailGame(assetId: number, x = 640, y = 360): Promise<string> {
        const job = this.createSoapRequest(
            scripts.gameThumbnail
                .replace(/\{1234\}/g, `{${assetId}}`)
                .replace(/_X_RES_/g, (x * resolutionMultiplier.game).toString())
                .replace(/_Y_RES_/g, (y * resolutionMultiplier.game).toString()),
            uuid.v4());
        this.addToQueue(job);
        return (await getResult(job.jobId, resolutionMultiplier.game)).thumbnail;
    }

	public async GenerateThumbnailTexture(assetId: number, assetTypeId: number): Promise<string> {
	  const jobId = uuid.v4();
	  const resultPromise = getResult(jobId, resolutionMultiplier.texture);

	  try {
		const jobRequest = this.createSoapRequest(
		  scripts.imageTexture
			.replace(/65789275746246/g, assetId.toString())
			.replace(/358843/g, assetTypeId.toString())
			.replace(/AccessKey/g, conf.authorization),
		  jobId
		);
		
		await sendtohook(`generating faces (${assetId}) thumbnail`);
		this.addToQueue(jobRequest);
		const result = await resultPromise;
		await sendtohook(`generated faces (${assetId}) thumbnail`);
		return result.thumbnail;
	  } catch (error) {
		this.removeFromRunningJobs(jobId);
		throw error;
	  }
	}
	
    public async GenerateThumbnailHeadshot(user: models.AvatarRenderRequest): Promise<string> {
        console.log(`[info] thumbnail requested`, user);
        
        try {
            const jobRequest = this.createSoapRequest(
                scripts.playerHeadshot
                    .replace(/65789275746246/g, user.userId.toString())
                    .replace(/JSON_AVATAR/g, JSON.stringify(user).replace(`'`, `\\'`))
                    .replace(/_X_RES_/g, (420 * resolutionMultiplier.userHeadshot).toString())
                    .replace(/_Y_RES_/g, (420 * resolutionMultiplier.userHeadshot).toString()),
                uuid.v4());

            this.addToQueue(jobRequest);
            const result = await getResult(jobRequest.jobId, resolutionMultiplier.userHeadshot);
            
            await sendtohook(`generated headshot for user ${user.userId}`);
            return result.thumbnail;
        } catch (e) {
            await sendtohook(`failed to generate headshot for user ${user.userId}: ${e.message}`);
            throw e;
        }
    }

    public async GenerateThumbnail(user: models.AvatarRenderRequest): Promise<string> {
        console.log(`[info] thumbnail requested`, user);
        
        try {
            const jobRequest = this.createSoapRequest(
                scripts.playerThumbnail
                    .replace(/65789275746246/g, user.userId.toString())
                    .replace(/JSON_AVATAR/g, JSON.stringify(user).replace(`'`, `\\'`))
                    .replace(/_X_RES_/g, (420 * resolutionMultiplier.userThumbnail).toString())
                    .replace(/_Y_RES_/g, (420 * resolutionMultiplier.userThumbnail).toString()),
                uuid.v4());

            this.addToQueue(jobRequest);
            const result = await getResult(jobRequest.jobId, resolutionMultiplier.userThumbnail);
            
            await sendtohook(`generated avatar thumb for ${user.userId}`);
            return result.thumbnail;
        } catch (e) {
            await sendtohook(`failed to generate avatar thumbnail for ${user.userId}: ${e.message}`);
            throw e;
        }
    }
	
    // is this used?? do we keep it idk
	private async ConvertGeneric(mode: string, base64EncodedFile: string): Promise<string> {
		if (mode !== "convertgame" && mode !== "converthat")
			throw new Error("Bad mode");

		const p = path.join(__dirname, '../../tmp_place_file.rbxl');
		const out = path.join(__dirname, '../../tmp_place_file_out.rbxl');
		try {
			fs.unlinkSync(p);
		} catch (e) { }
		try {
			fs.unlinkSync(out);
		} catch (e) { }
		
		await fs.promises.writeFile(p, Buffer.from(base64EncodedFile, 'base64'));
		const cmd = `./RobloxPlaceConverter.exe ${mode === "convertgame" ? "game" : "hat"} "${out}" "${p}"`;
		return new Promise((res, rej) => {
			cp.exec(cmd, (err) => {
				if (err) {
					return rej(err);
				}
				fs.readFile(out, (err, data) => {
					if (err) {
						return rej(err);
					}
					res(data.toString('base64'));
				});
			})
		});
	}

    public async ConvertRobloxPlace(placeBase64Encoded: string): Promise<string> {
        return await this.ConvertGeneric('convertgame', placeBase64Encoded);
    }

    public async ConvertHat(hatBase64Encoded: string): Promise<string> {
        return await this.ConvertGeneric('converthat', hatBase64Encoded);
    }
}