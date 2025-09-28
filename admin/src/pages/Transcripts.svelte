<script lang="ts">
	import { link } from "svelte-routing";
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	import moment from "../lib/moment";

	let TData = [];
	let offset = 0;
	let limit = 10;
	let disabled = false;

	const Search = () => {
		disabled = true;
		request
			.get(`/transcripts/tickets?limit=${limit}&offset=${offset}`)
			.then((res) => {
				TData = res.data.data;
			})
			.finally(() => {
				disabled = false;
			});
	}

	$: {
		Search();
	}
</script>

<svelte:head>
	<title>Transcripts</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12 col-md-6">
			<h1>Transcripts</h1>
		</div>
		<div class="col-12 col-md-6 col-lg-3">
			<label for="limit">LIMIT</label>
			<select {disabled} id="limit" class="form-control" on:change={(e) => {
				limit = parseInt(e.currentTarget.value, 10);
			}}>
				<option value="10">10</option>
				<option value="50">50</option>
				<option value="100">100</option>
				<option value="500">500</option>
			</select>
		</div>
		<div class="col-12 col-md-12 col-lg-3">
			<p class="mb-0 mt-0">&emsp;</p>
			<button class="btn btn-primary w-100" {disabled} on:click={() => {
				Search();
			}}>Refresh</button>
		</div>
		<div class="col-12">
			<table class="table">
				<thead>
					<tr>
						<th>Ticket #</th>
						<th>Name</th>
						<th>Last Message Time</th>
						<th>Message Count</th>
					</tr>
				</thead>
				<tbody>
					{#each TData as ticket}
						<tr>
							<td>
								<a use:link href={`/admin/transcripts/${ticket.ticket_id}`}>
									{ticket.ticket_id}
								</a>
							</td>
							<td>{ticket.name}</td>
							<td>{moment(ticket.last_message_time).format("MMM DD YYYY, h:mm A")}</td>
							<td>{ticket.message_count.toLocaleString()}</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
		<div class="col-12">
			<nav aria-label="Page navigation example">
				<ul class="pagination">
					<li class={`page-item${disabled || !offset ? " disabled" : ""}`}>
						<a
							class="page-link"
							href="#!"
							on:click={(e) => {
								e.preventDefault();
								if (offset >= limit) {
									offset -= limit;
									Search();
								}
							}}>Previous</a
						>
					</li>
					<li class="page-item active">
						<a
							class="page-link"
							href="#!"
							on:click={(e) => {
								e.preventDefault();
							}}>{(offset / limit + 1).toLocaleString()}</a
						>
					</li>
					<li class={`page-item${disabled || (TData && TData.length < limit) ? " disabled" : ""}`}>
						<a
							class="page-link"
							href="#!"
							on:click={(e) => {
								e.preventDefault();
								offset += limit;
								Search();
							}}>Next</a
						>
					</li>
				</ul>
			</nav>
		</div>
	</div>
</Main>