<script lang="ts">
	import { navigate } from "svelte-routing";
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	let disabled = false;
	let errorMessage: string | undefined;
	import * as rank from "../stores/rank";
    rank.promise.then(() => {
        if (!rank.hasPermission("CreateAsset")) {
            errorMessage = "You don't have permission to create assets.";
            disabled = true;
        }
    });
	let assetTypeId = "1";
	let offsaleOffsetValue = "";
	let offsaleOffsetUnit = "seconds"; 
</script>

<svelte:head>
	<title>Create Item</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>Create Item</h1>
			{#if errorMessage}
				<p class="err">{errorMessage}</p>
			{/if}
		</div>
		<div class="col-6">
			<label for="name">Name</label>
			<input type="text" class="form-control" id="name" {disabled} required />
		</div>
		<div class="col-6">
			<label for="description">Description (Optional)</label>
			<input type="text" class="form-control" id="description" />
		</div>
		<div class="col-6">
			<label for="assettype">Type</label>
			<select class="form-control" bind:value={assetTypeId}>
				{#await request.get("/asset/types") then data}
					{#each Object.getOwnPropertyNames(data.data) as element}
						{#if !isNaN(parseInt(element))}
							<option value={element}>{data.data[element]}</option>
						{/if}
					{/each}
				{/await}
			</select>
		</div>
		<div class="col-6">
			<label for="assetgenre">Genre</label>
			<select class="form-control" id="assetgenre">
				{#await request.get("/asset/genres") then data}
					{#each Object.getOwnPropertyNames(data.data) as element}
						{#if !isNaN(parseInt(element))}
							<option value={element}>{data.data[element]}</option>
						{/if}
					{/each}
				{/await}
			</select>
		</div>
		{#if assetTypeId === "32"}
			<div class="col-12 mt-4 mb-4">
				<label for="rbxm">CSV of Package Asset IDs (e.g. "1,2,3")</label>
				<input type="text" class="form-control" id="assetIdsForPackage" />
			</div>
		{:else}
			<div class="col-12 mt-4 mb-4">
				<label for="rbxm">.RBXM File</label>
				<input type="file" class="form-control" id="rbxm" required />
			</div>
		{/if}
		<div class="col-12">
			<h3>Economy</h3>
			<div class="row">
				<div class="col-4">
					<label for="name">Price (Optional)</label>
					<input type="text" class="form-control" id="price" {disabled} />
				</div>
				<div class="col-2 mt-4">
					<div class="form-check">
						<input type="checkbox" class="form-check-input" id="is_for_sale" />
						<label class="form-check-label" for="is_for_sale">For Sale</label>
					</div>
					<div class="form-check mt-2">
						<input 
							type="checkbox" 
							class="form-check-input" 
							id="is_visible" 
							checked={true}
						/>
						<label class="form-check-label" for="is_visible">Visible</label>
					</div>
				</div>
				<div class="col-6">
					<label for="description">Limited Status</label>
					<select class="form-control" id="limited-status">
						<option value="false">Not Limited</option>
						<option value="limited">Limited</option>
						<option value="limited_u">Limited Unique</option>
					</select>
				</div>
				<div class="col-6">
					<label for="description">Max Copy Count (optional)</label>
					<input type="text" class="form-control" id="max-copies" />
				</div>
				<div class="col-6">
					<label for="description">Offsale After (optional)</label>
					<div class="row">
						<div class="col-6">
							<input type="number" class="form-control" id="offsale-offset-value" placeholder="e.g., 30" bind:value={offsaleOffsetValue} />
						</div>
						<div class="col-6">
							<select class="form-control" id="offsale-offset-unit" bind:value={offsaleOffsetUnit}>
								<option value="seconds">Seconds</option>
								<option value="minutes">Minutes</option>
								<option value="hours">Hours</option>
								<option value="days">Days</option>
								<option value="weeks">Weeks</option>
								<option value="months">Months</option>
							</select>
						</div>
					</div>
				</div>
			</div>
		</div>
		<div class="col-12 mt-4">
			<button
				class="btn btn-success"
				{disabled}
				on:click={(e) => {
					e.preventDefault();
					if (disabled) {
						return;
					}
					let bodyFormData = new FormData();
					bodyFormData.append("name", document.getElementById("name").value);
					bodyFormData.append("description", document.getElementById("description").value);
					bodyFormData.append("assetTypeId", assetTypeId);
					bodyFormData.append("genre", document.getElementById("assetgenre").value);
					bodyFormData.append("price", document.getElementById("price").value);
					bodyFormData.append("isVisible", (document.getElementById("is_visible").checked).toString());
					let offsaleOffsetValue = document.getElementById("offsale-offset-value").value;
					let offsaleOffsetUnit = document.getElementById("offsale-offset-unit").value;
					let limStatus = document.getElementById("limited-status").value;
					if (limStatus === "limited" || limStatus === "limited_u") {
						bodyFormData.append("isLimited", "true");
					}
					if (limStatus === "limited_u") {
						bodyFormData.append("isLimitedUnique", "true");
					}
					let maxCopies = document.getElementById("max-copies").value;
					if (maxCopies !== "") {
						bodyFormData.append("maxCopies", maxCopies);
					}
					bodyFormData.append("isForSale", document.getElementById("is_for_sale").checked ? "true" : "false");
					if (document.getElementById("rbxm")) {
						bodyFormData.append("rbxm", document.getElementById("rbxm").files[0]);
					}
					if (assetTypeId === "32") {
						bodyFormData.append("packageAssetIds", document.getElementById('assetIdsForPackage').value);
					}
					if (offsaleOffsetValue) {
						const offset = parseInt(offsaleOffsetValue);
						if (isNaN(offset) || offset <= 0) {
							errorMessage = "Offsale offset must be a positive number";
							return;
						}

						bodyFormData.append("offsaleOffsetValue", offset.toString());
						bodyFormData.append("offsaleOffsetUnit", offsaleOffsetUnit);
					}
					disabled = true;
					request
						.post("/asset/create", bodyFormData)
						.then((d) => {
							window.location.href = `/catalog/${d.data.assetId}/--`;
						})
						.catch((e) => {
							errorMessage = e.message;
						})
						.finally(() => {
							disabled = false;
						});
				}}>Create Asset</button
			>
		</div>
	</div>
</Main>

<style>
	p.err {
		color: red;
	}
</style>
