<script lang="ts">
    import { onMount } from "svelte";
    import Main from "../components/templates/Main.svelte";
    import request from "../lib/request";
    import * as rank from "../stores/rank";

    let servers: any[] = [];
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
			const response = await request.get("/game-servers/running");

			if (Array.isArray(response) && response.length > 0) {
				servers = response;
			} else {
				servers = [];
				
				if (response && Array.isArray(response.data)) {
					servers = response.data;
				}
				else if (response && Array.isArray(response.servers)) {
					servers = response.servers;
				}
			}
		} catch (error) {
			console.error("Failed to load servers:", error);
			errorMsg = error.message || "Failed to load servers";
			servers = [];
		} finally {
			loading = false;
		}
	}

    async function Shutdown(serverId: string) {
        if (!confirm('Are you sure you want to shutdown this server? All players will be disconnected.')) {
            return;
        }

        try {
            loading = true;
            errorMsg = undefined;
            const response = await request.post("/game-servers/shutdown", { ServerId: serverId });
            
            if (response.Success) {
                successMsg = response.Message;
				await new Promise(resolve => setTimeout(resolve, 1500));
                await Load();
				await Load();
            } else {
                errorMsg = response.Message;
            }
        } catch (error) {
            console.error("Failed to shutdown server:", error);
            errorMsg = error.message || "Failed to shutdown server";
        } finally {
            loading = false;
        }
    }

    async function CleanOrphaned() {
        if (!confirm('Clean up orphaned servers? This will remove servers that are in the DB but have no process open')) {
            return;
        }

        try {
            loading = true;
            errorMsg = undefined;
            const response = await request.post("/game-servers/cleanup-orphaned");
            
            if (response.Success) {
                successMsg = response.Message;
                await Load();
				await Load();
            } else {
                errorMsg = response.Message;
            }
        } catch (error) {
            console.error("Failed to cleanup orphaned servers:", error);
            errorMsg = error.message || "Failed to cleanup orphaned servers";
        } finally {
            loading = false;
        }
    }

    onMount(() => {
        Load();
    });
</script>

<svelte:head>
    <title>Running Game Servers</title>
</svelte:head>

<Main>
    <div class="row">
        <div class="col-12">
            <h1>Running Game Servers</h1>
            
            {#if errorMsg}
                <div class="alert alert-danger">{errorMsg}</div>
            {/if}
            
            {#if successMsg}
                <div class="alert alert-success">{successMsg}</div>
            {/if}
            
            <div class="d-flex justify-content-between align-items-center mb-3">
                <div class="btn-group">
                    <button class="btn btn-secondary btn-sm" on:click={Load} disabled={loading}>
                        {#if loading}
                            <span class="spinner-border spinner-border-sm" role="status"></span>
                            Refreshing...
                        {:else}
                            Refresh
                        {/if}
                    </button>
                    <button class="btn btn-warning btn-sm" on:click={CleanOrphaned} disabled={loading}>
                        Cleanup Orphaned
                    </button>
                </div>
            </div>
            
            {#if loading && servers.length === 0}
                <div class="text-center py-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-2">Loading...</p>
                </div>
            {:else if servers.length === 0}
                <div class="alert alert-info">No game servers are currently running</div>
            {:else}
                <div class="table-responsive">
                    <table class="table table-striped table-hover">
                        <thead>
                            <tr>
                                <th>Job ID</th>
                                <th>Place</th>
                                <th>Port</th>
                                <th>Players</th>
                                <th>Memory (MB)</th>
                                <th>Uptime</th>
                                <th>Last Ping</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {#each servers as server}
                                <tr>
                                    <td><code>{server.JobId}</code></td>
                                    <td>{server.PlaceName} (ID: {server.PlaceId})</td>
                                    <td>{server.Port}</td>
                                    <td>{server.PlayerCount}</td>
                                    <td>{server.MemoryUsage?.toFixed(1)}</td>
                                    <td>
                                        {#if server.StartTime}
                                            {Math.round((Date.now() - new Date(server.StartTime).getTime()) / 60000)} min
                                        {:else}
                                            Unknown
                                        {/if}
                                    </td>
                                    <td>
                                        {#if server.LastPing}
                                            {Math.round((Date.now() - new Date(server.LastPing).getTime()) / 1000)} sec ago
                                        {:else}
                                            Never
                                        {/if}
                                    </td>
                                    <td>
                                        <button 
                                            class="btn btn-danger btn-sm"
                                            on:click={() => Shutdown(server.JobId)}
                                            disabled={loading}
                                            title="Shutdown"
                                        >
                                            Shutdown
                                        </button>
                                    </td>
                                </tr>
                            {/each}
                        </tbody>
                    </table>
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
    .btn-sm {
        padding: 0.25rem 0.5rem;
        font-size: 0.875rem;
    }
    .spinner-border-sm {
        width: 1rem;
        height: 1rem;
    }
</style>