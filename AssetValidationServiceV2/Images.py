from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse
from pydub import AudioSegment
import aiohttp
import os
import tempfile
import magic

app = FastAPI()

Webhook = "https://discord.com/api/webhooks/1416593295756492801/UN1vlLt_gT1Oz2WSxRXALBy5rTW63NzrOJCad_7ci67zAVwU9jX-Myq97_m4Lrlc64Cz"

AudioSignatures = {
    b"ID3": ".mp3",
    b"\xff\xfb": ".mp3",
    b"RIFF": ".wav",
    b"OggS": ".ogg",
}

async def SendAudioToDiscord(file_path, ext):
    async with aiohttp.ClientSession() as session:
        form = aiohttp.FormData()
        form.add_field(
            "file",
            open(file_path, "rb"),
            filename=f"imageaudio{ext}",
            content_type="application/octet-stream"
        )
        form.add_field("content", "found bad audio in image")

        async with session.post(Webhook, data=form) as resp:
            if resp.status not in (200, 204):
                print(f"Failed to send to Discord: {resp.status}")

def Validate(File):
    try:
        Type = magic.from_file(File, mime=True)
        if not Type.startswith('audio/'):
            return False
        
        audio = AudioSegment.from_file(File)
        
        if len(audio) <= 0:
            return False

        if len(audio) < 100:
            return False

        if audio.max == 0:
            return False
            
        return True
        
    except Exception as e:
        print(f"image validation failed, most likely false detection: {e}")
        return False
        
Images = (".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".gif")
@app.post("/validateImage")
async def ValidateImage(file: UploadFile = File(...)):
    if not file.filename.lower().endswith(Images):
        raise HTTPException(status_code=500, detail=f"Only these image files are allowed: {', '.join(Images)}")

    content = await file.read()
    found = False
    pos = 0
    FileCount = 0

    while pos < len(content):
        match = None
        ext = None
        for sig, e in AudioSignatures.items():
            if content.startswith(sig, pos):
                match = sig
                ext = e
                break
                
        if match:
            FileCount += 1
            end_marker = b"IEND"
            end_pos = content.find(end_marker, pos)
            if end_pos == -1:
                end_pos = len(content)
            else:
                end_pos += len(end_marker)

            chunk = content[pos:end_pos]

            with tempfile.NamedTemporaryFile(delete=False, suffix=ext) as tmp_file:
                tmp_file.write(chunk)
                TempName = tmp_file.name
                
            if len(chunk) < 1024:
                continue

            if Validate(TempName):
                found = True
                await SendAudioToDiscord(TempName, ext)
                os.remove(TempName)
            else:
                os.remove(TempName)

            pos = end_pos
        else:
            pos += 1

        if found:
            raise HTTPException(status_code=400, detail="Found audio in image")
            print("found audio")

    return JSONResponse({"status": not found})

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=3030)
