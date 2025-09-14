<script lang="ts">
    import { onMount } from "svelte";
    import Main from "../components/templates/Main.svelte";
    import request from "../lib/request";
    import * as rank from "../stores/rank";

    let rccProcesses: any[] = [];
    let loading = false;
    let errorMsg: string | undefined;
    let successMsg: string | undefined;

    rank.promise.then(() => {
        if (!rank.hasPermission("ManageRCCInstances")) {
            errorMsg = "You don't have permission to manage RCC instances";
        }
    });

    async function Load() {
        try {
            loading = true;
            errorMsg = undefined;
            const response = await request.get("RCC-Internal/rcc-processes");

            if (Array.isArray(response)) {
                rccProcesses = response;
            } else if (response && Array.isArray(response.data)) {
                rccProcesses = response.data;
            } else if (response && response.Success && Array.isArray(response.Data)) {
                rccProcesses = response.Data;
            } else {
                rccProcesses = [];
                console.warn('Unexpected response format:', response);
            }
        } catch (error) {
            console.error("Failed to load RCC processes:", error);
            errorMsg = error.response?.data?.message || error.message || "Failed to load RCC processes";
            rccProcesses = [];
        } finally {
            loading = false;
        }
    }

    async function Kill(processId: number) {
        if (!confirm('Are you sure you want to kill this RCC process? This will shutdown any running game server attached to it.')) {
            return;
        }

        try {
            loading = true;
            errorMsg = undefined;
            const response = await request.post("RCC-Internal/rcc-processes/kill", { ProcessId: processId });
            
            if (response.Success) {
                successMsg = response.Message;
				await new Promise(resolve => setTimeout(resolve, 2000));
                await Load();
            } else {
                errorMsg = response.Message;
            }
        } catch (error) {
            console.error("Failed to kill process:", error);
            errorMsg = error.message || "Failed to kill process";
        } finally {
            loading = false;
        }
    }

    onMount(() => {
        Load();
    });
</script>

<svelte:head>
    <title>RCC Instances</title>
</svelte:head>

<Main>
    <div class="row">
        <div class="col-12">
            <h1>RCC Instances</h1>
            
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
            
            {#if loading && rccProcesses.length === 0}
                <div class="text-center py-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-2">Loading processes...</p>
                </div>
            {:else if rccProcesses.length === 0}
                <div class="alert alert-info">No RCC instances are currently running</div>
            {:else}
                <div class="table-responsive">
                    <table class="table table-striped table-hover">
                        <thead>
                            <tr>
                                <th>Process ID</th>
                                <th>Job ID</th>
                                <th>Memory (MB)</th>
                                <th>Uptime</th>
                                <th>CPU Time</th>
                                <th>Status</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {#each rccProcesses as process}
                                <tr class={process.HasExited ? 'table-secondary' : ''}>
                                    <td>{process.ProcessId}</td>
                                    <td><code>{process.JobId || 'N/A'}</code></td>
                                    <td>{process.MemoryUsageMB?.toFixed(1)}</td>
                                    <td>
                                        {#if process.StartTime}
                                            {Math.round((Date.now() - new Date(process.StartTime).getTime()) / 60000)} min
                                        {:else}
                                            Unknown
                                        {/if}
                                    </td>
                                    <td>
                                        {#if process.TotalProcessorTime}
                                            {Math.round(process.TotalProcessorTime.totalMinutes)} min
                                        {:else}
                                            Unknown
                                        {/if}
                                    </td>
                                    <td>
                                        {#if process.HasExited}
                                            <span class="badge bg-danger">Exited</span>
                                        {:else if process.Responding}
                                            <span class="badge bg-success">Running</span>
                                        {:else}
                                            <span class="badge bg-warning">Not Responding</span>
                                        {/if}
                                    </td>
                                    <td>
                                        {#if !process.HasExited}
                                            <button 
                                                class="btn btn-danger btn-sm"
                                                on:click={() => Kill(process.ProcessId)}
                                                disabled={loading}
                                                title="Kill"
                                            >
                                                Kill
                                            </button>
                                        {:else}
                                            <span class="text-muted">Exited</span>
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
</style>