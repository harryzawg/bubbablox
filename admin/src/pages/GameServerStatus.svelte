<script lang="ts">
    import { onMount } from "svelte";
    import Main from "../components/templates/Main.svelte";
    import request from "../lib/request";
    import * as rank from "../stores/rank";

    interface GameServerStatus {
        jobRccs: Array<{
            jobId: string;
            processId: number;
            processName: string;
            hasExited: boolean;
            startTime: Date;
            memoryUsage: string;
            memoryUsageMB?: number;
            cpuTime?: string;
            threads?: number;
            handleCount?: number;
            responding?: boolean;
        }>;
        currentGameServerPorts: Array<{
            jobId: string;
            port: number;
            playerCount?: number;
        }>;
        currentPlaceIdsInUse: Array<{
            placeId: number;
            jobIds: string[];
            totalServers?: number;
        }>;
        mainRCCPortsInUse: Array<{
            processId: number;
            port: number;
            processName?: string;
        }>;
        currentPlayersInGame: Array<{
            userId: number;
            placeId: number;
        }>;
        statistics?: {
            TotalRunningServers: number;
            TotalPlayersInGame: number;
            TotalPortsUsed: number;
            TotalPlacesRunning: number;
            TotalRCCs: number;
        };
        DBServers?: Array<{
            serverId: string;
            placeId: number;
            port: number;
            createdAt: Date;
            lastPing: Date;
            secondsSinceLastPing: number;
        }>;
        Metrics?: {
            timestamp: Date;
            processMemoryMB: number;
            totalThreads: number;
        };
    }

    let status: GameServerStatus | null = null;
    let loading = false;
    let errorMsg: string | undefined;
    let successMsg: string | undefined;

    rank.promise.then(() => {
        if (!rank.hasPermission("ManageRunningGameServers")) {
            errorMsg = "You don't have permission to manage game servers";
        }
    });

    async function Load() {
        try {
            loading = true;
            errorMsg = undefined;
            const response = await request.get("game-servers/status");
            
            console.log("status response:", response);

            if (response) {
                status = response.data;
                console.log("status set to:", status);
            } else {
                status = null;
                console.warn('bad response:', response);
            }
        } catch (error) {
            console.error("failed to load game status:", error);
            errorMsg = error.response?.data?.message || error.message || "Failed to load GS status";
            status = null;
        } finally {
            loading = false;
        }
    }

    function FormatMem(memoryMB: number): string {
        return memoryMB.toFixed(1) + " MB";
    }

    function FormatUp(startTime: Date): string {
        const uptimeMs = Date.now() - new Date(startTime).getTime();
        const minutes = Math.floor(uptimeMs / 60000);
        const hours = Math.floor(minutes / 60);
        const days = Math.floor(hours / 24);
        
        if (days > 0) return `${days}d ${hours % 24}h`;
        if (hours > 0) return `${hours}h ${minutes % 60}m`;
        return `${minutes}m`;
    }

    onMount(() => {
        Load();
    });
</script>

<svelte:head>
    <title>Game Server Status</title>
</svelte:head>

<Main>
    <div class="row">
        <div class="col-12">
            <h1>Game Server Status</h1>
            {#if errorMsg}
                <div class="alert alert-danger">{errorMsg}</div>
            {/if}        
            {#if successMsg}
                <div class="alert alert-success">{successMsg}</div>
            {/if}
            <div class="d-flex justify-content-between align-items-center mb-3">
                <button class="btn btn-secondary btn-sm" on:click={Load} disabled={loading}>
                    {#if loading}
                        <span class="spinner-border spinner-border-sm" role="status"></span>
                        Refreshing...
                    {:else}
                        Refresh
                    {/if}
                </button>
            </div>
            {#if loading && !status}
                <div class="text-center py-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-2">Loading...</p>
                </div>
            {:else if !status}
                <div class="alert alert-info">Nothing is running</div>
            {:else}
                {#if status.statistics}
                    <div class="row mb-4">
                        <div class="col-12">
                            <h4>Statistics</h4>
                            <div class="row">
                                <div class="col-md-2 col-6 mb-2">
                                    <div class="card bg-primary text-white text-center">
                                        <div class="card-body p-2">
                                            <h5 class="card-title mb-0">{status.statistics.TotalRunningServers}</h5>
                                            <small>Active Servers</small>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-2 col-6 mb-2">
                                    <div class="card bg-success text-white text-center">
                                        <div class="card-body p-2">
                                            <h5 class="card-title mb-0">{status.statistics.TotalPlayersInGame}</h5>
                                            <small>Players in Game</small>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-2 col-6 mb-2">
                                    <div class="card bg-info text-white text-center">
                                        <div class="card-body p-2">
                                            <h5 class="card-title mb-0">{status.statistics.TotalPortsUsed}</h5>
                                            <small>Ports in Use</small>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-2 col-6 mb-2">
                                    <div class="card bg-warning text-white text-center">
                                        <div class="card-body p-2">
                                            <h5 class="card-title mb-0">{status.statistics.TotalPlacesRunning}</h5>
                                            <small>Places Running</small>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-2 col-6 mb-2">
                                    <div class="card bg-secondary text-white text-center">
                                        <div class="card-body p-2">
                                            <h5 class="card-title mb-0">{status.statistics.TotalRCCs}</h5>
                                            <small>RCC instances</small>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                {/if}
                <div class="row mb-4">
                    <div class="col-12">
                        <h4>RCC instances ({status.jobRccs?.length || 0})</h4>
                        {#if !status.jobRccs || status.jobRccs.length === 0}
                            <div class="alert alert-info">No RCC instances are running</div>
                        {:else}
                            <div class="table-responsive">
                                <table class="table table-striped table-hover">
                                    <thead>
                                        <tr>
                                            <th>Process ID</th>
                                            <th>Job ID</th>
                                            <th>Memory</th>
                                            <th>Uptime</th>
                                            <th>Threads</th>
                                            <th>Status</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {#each status.jobRccs as process}
                                            <tr class={process.hasExited ? 'table-secondary' : ''}>
                                                <td>{process.processId}</td>
                                                <td><code>{process.jobId || 'N/A'}</code></td>
                                                <td>
                                                    {#if process.memoryUsageMB}
                                                        {FormatMem(process.memoryUsageMB)}
                                                    {:else if process.memoryUsage}
                                                        {process.memoryUsage}
                                                    {:else}
                                                        Unknown
                                                    {/if}
                                                </td>
                                                <td>
                                                    {#if process.startTime}
                                                        {FormatUp(process.startTime)}
                                                    {:else}
                                                        Unknown
                                                    {/if}
                                                </td>
                                                <td>{process.threads || 'N/A'}</td>
                                                <td>
                                                    {#if process.hasExited}
                                                        <span class="badge bg-danger">Exited</span>
                                                    {:else if process.responding}
                                                        <span class="badge bg-success">Running</span>
                                                    {:else}
                                                        <span class="badge bg-warning">Not Responding</span>
                                                    {/if}
                                                </td>
                                            </tr>
                                        {/each}
                                    </tbody>
                                </table>
                            </div>
                        {/if}
                    </div>
                </div>
                <div class="row mb-4">
                    <div class="col-12">
                        <h4>Game Servers ({status.currentGameServerPorts?.length || 0})</h4>
                        {#if !status.currentGameServerPorts || status.currentGameServerPorts.length === 0}
                            <div class="alert alert-info">No game servers running</div>
                        {:else}
                            <div class="table-responsive">
                                <table class="table table-striped table-hover">
                                    <thead>
                                        <tr>
                                            <th>Job ID</th>
                                            <th>Port</th>
                                            <th>Players</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {#each status.currentGameServerPorts as server}
                                            <tr>
                                                <td><code>{server.jobId}</code></td>
                                                <td>{server.port}</td>
                                                <td>{server.playerCount || 0}</td>
                                            </tr>
                                        {/each}
                                    </tbody>
                                </table>
                            </div>
                        {/if}
                    </div>
                </div>
                <div class="row mb-4">
                    <div class="col-12">
                        <h4>Places Running ({status.currentPlaceIdsInUse?.length || 0})</h4>
                        {#if !status.currentPlaceIdsInUse || status.currentPlaceIdsInUse.length === 0}
                            <div class="alert alert-info">No places currently running</div>
                        {:else}
                            <div class="table-responsive">
                                <table class="table table-striped table-hover">
                                    <thead>
                                        <tr>
                                            <th>Place ID</th>
                                            <th>Servers</th>
                                            <th>Job IDs</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {#each status.currentPlaceIdsInUse as place}
                                            <tr>
                                                <td>{place.placeId}</td>
                                                <td>{place.totalServers || place.jobIds.length}</td>
                                                <td>
                                                    {#each place.jobIds as jobId, index}
                                                        <code>{jobId}{index < place.jobIds.length - 1 ? ', ' : ''}</code>
                                                    {/each}
                                                </td>
                                            </tr>
                                        {/each}
                                    </tbody>
                                </table>
                            </div>
                        {/if}
                    </div>
                </div>
                <div class="row">
                    <div class="col-12">
                        <h4>Players in Game ({status.currentPlayersInGame?.length || 0})</h4>
                        {#if !status.currentPlayersInGame || status.currentPlayersInGame.length === 0}
                            <div class="alert alert-info">No players currently in game</div>
                        {:else}
                            <div class="table-responsive">
                                <table class="table table-striped table-hover">
                                    <thead>
                                        <tr>
                                            <th>User ID</th>
                                            <th>Place ID</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {#each status.currentPlayersInGame as player}
                                            <tr>
                                                <td>{player.userId}</td>
                                                <td>{player.placeId}</td>
                                            </tr>
                                        {/each}
                                    </tbody>
                                </table>
                            </div>
                        {/if}
                    </div>
                </div>
            {/if}
        </div>
    </div>
</Main>

<style>
    .alert {
        margin-bottom: 1rem;
    }
    .table-responsive {
        overflow-x: auto;
    }
    code {
        background-color: #f8f9fa;
        padding: 0.2rem 0.4rem;
        border-radius: 0.25rem;
        font-size: 0.875rem;
    }
    .badge {
        font-size: 0.75rem;
    }
    .btn-sm {
        padding: 0.25rem 0.5rem;
        font-size: 0.875rem;
    }
    .spinner-border-sm {
        width: 1rem;
        height: 1rem;
    }
    .table-secondary {
        opacity: 0.6;
    }
    .card {
        min-height: 60px;
    }
    .card-title {
        font-size: 1.2rem;
    }
</style>