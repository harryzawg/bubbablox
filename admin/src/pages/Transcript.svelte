<script lang="ts">
	import { link, Router } from "svelte-routing";
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	import moment from "../lib/moment";

	export let ticketId: string;

	let Messages = [];
	let offset = 0;
	let limit = 100;
	let disabled = false;

	const Search = () => {
		disabled = true;
		request
			.get(`/transcripts/messages?ticketId=${ticketId}&limit=${limit}&offset=${offset}`)
			.then((res) => {
				Messages = res.data.data;
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
	<title>Transcript #{ticketId}</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12 col-md-6">
			<h1>Transcript #{ticketId}</h1>
		</div>
		<div class="col-12 col-md-6 col-lg-3">
			<label for="limit">LIMIT</label>
			<select {disabled} id="limit" class="form-control" on:change={(e) => {
				limit = parseInt(e.currentTarget.value, 10);
			}}>
				<option value="50">50</option>
				<option value="100">100</option>
				<option value="200">200</option>
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
			<a use:link href="/admin/transcripts" class="btn btn-secondary mb-3">Back</a>
		</div>
		<div class="col-12">
			<table class="table">
				<thead>
					<tr>
						<th>#</th>
						<th>User ID</th>
						<th>Username</th>
						<th>Discord ID</th>
						<th>Message</th>
						<th>Time</th>
					</tr>
				</thead>
				<tbody>
					{#each Messages as message}
						<tr>
							<td>{message.id}</td>
							<td>
								<a use:link href={`/admin/manage-user/${message.user_id}`}>
									{message.user_id}
								</a>
							</td>
							<td>{message.username || "Unknown"}</td>
							<td>{message.discord_id}</td>
							<td style="max-width: 300px; word-wrap: break-word;">{message.message}</td>
							<td>{moment(message.created_at).format("MMM DD YYYY, h:mm A")}</td>
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
					<li class={`page-item${disabled || (Messages && Messages.length < limit) ? " disabled" : ""}`}>
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