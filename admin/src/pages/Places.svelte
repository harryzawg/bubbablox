<script lang="ts">
	import { link } from "svelte-routing";
	import Confirm from '../components/modal/Confirm.svelte';
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	import moment from "../lib/moment";

	let placesData = [];
	let sortMode = "desc";
	let offset = 0;
	let limit = 10;
	let disabled = false;
	let searchQuery = "";

	let modalBody: string|undefined;
	let modalCb: (didClickYes: boolean) => void|undefined;
	let modalVisible: boolean = false;

	const searchPlaces = () => {
		disabled = true;
		request
			.get(`/games/list?limit=${limit}&offset=${offset}&query=${encodeURIComponent(searchQuery)}`)
			.then((res) => {
				placesData = res.data;
			})
			.finally(() => {
				disabled = false;
			});
	}

	$: {
		searchPlaces();
	}
</script>

<svelte:head>
	<title>Places</title>
</svelte:head>

<Main>
	{#if modalVisible}
		<Confirm
			title="Confirm"
			message={modalBody}
			cb={(e) => {
				modalVisible = false;
				modalCb(e);
			}}
		/>
	{/if}
	<div class="row mb-3">
		<div class="col-12 col-md-2">
			<h1>Places</h1>
		</div>
		<div class="col-12 col-md-6 col-lg-3">
			<label>LIMIT</label>
			<select {disabled} class="form-control" on:change={(e) => {
				limit = parseInt(e.currentTarget.value, 10);
			}}>
				<option value="10">10</option>
				<option value="50">50</option>
				<option value="100">100</option>
			</select>
		</div>
		<div class="col-12 col-md-6 col-lg-3">
			<label>SEARCH PLACES</label>
			<input {disabled} class="form-control" type="text" bind:value={searchQuery} maxlength={64} />
		</div>
		<div class="col-12 col-md-3 mt-4">
			<button class="btn btn-primary w-100" {disabled} on:click={searchPlaces}>Search</button>
		</div>
	</div>

	<table class="table">
		<thead>
			<tr>
				<th>ID</th>
				<th>Name</th>
				<th>Description</th>
				<th>Visits</th>
				<th>Players</th>
				<th>Creator</th>
				<th>Year</th>
			</tr>
		</thead>
		<tbody>
			{#each placesData as place}
				<tr>
					<td><a use:link href={`/admin/place/${place.placeId}`}>{place.placeId}</a></td>
					<td><a use:link href={`/admin/place/${place.placeId}`}>{place.placeName}</a></td>
					<td>{place.description || "No description"}</td>
					<td>{place.visits.toLocaleString()}</td>
					<td>{place.currentPlayers}</td>
					<td><a use:link href={`/admin/manage-user/${place.creatorId}`}>{place.creatorName}</a></td>
					<td>{place.year}</td>
				</tr>
			{/each}
		</tbody>
	</table>

	<div class="d-flex justify-content-between mt-3">
		<button class="btn btn-secondary" {disabled} on:click={() => { if (offset >= limit) { offset -= limit; searchPlaces(); } }}>Previous</button>
		<span>Page {(offset / limit + 1).toLocaleString()}</span>
		<button class="btn btn-secondary" {disabled} on:click={() => { offset += limit; searchPlaces(); }}>Next</button>
	</div>
</Main>
