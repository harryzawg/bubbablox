<script lang="ts">
	import Permission from "../components/Permission.svelte";
	import Main from "../components/templates/Main.svelte";
	import { hasPermission } from "../stores/rank";
	import { getElementById } from "../lib/dom";
	import request from "../lib/request";
	let disabled = false;
	let errorMessage: string | undefined;
	import * as rank from "../stores/rank";
	import SaleHistory from "../components/SaleHistory.svelte";
	import ProductHistory from "../components/ProductHistory.svelte";
	let queryParams = new URLSearchParams(window.location.search);
	let assetId: number = parseInt(queryParams.get("assetId"), 10) || undefined;
	let dirtyAssetId: string = assetId ? assetId.toString() : '';
	let offsaleOffsetValue = "";
	let offsaleOffsetUnit = "seconds";
	
	interface IDetailsResponse {
		name: string;
		description: string;
		isForSale: boolean;
		isLimited: boolean;
		isLimitedUnique: boolean;
		priceRobux: number | null;
		priceTickets: number|null;
		serialCount: number | null;
		offsaleAt: string | null;
		isVisible: boolean;
	}
	
	let assetDetails: Partial<IDetailsResponse> = {};
	let latestFetch;
	
	$: {
		if (latestFetch) {
			clearTimeout(latestFetch);
		}
		if (assetId) {
			latestFetch = setTimeout(() => {
				disabled = true;
				request
					.get("/product/details?assetId=" + assetId)
					.then((d) => {
						if (d.data.isLimited || d.data.isLimitedUnique) {
							if (!hasPermission('MakeItemLimited')) {
								errorMessage = "You do not have permission to modify limited items.";
								disabled = false;
								return;
							}
						}
						errorMessage = null;
						assetDetails = d.data;

						offsaleOffsetValue = "";
						offsaleOffsetUnit = "seconds";
					})
					.finally(() => {
						disabled = false;
					});
			}, 1);
		}
	}
	
	function updateAssetDetails() {
		const name = getElementById("asset-name").value;
		const description = getElementById("asset-description").value;
		
		if (!name) {
			errorMessage = "Name cannot be empty";
			return;
		}

		disabled = true;
		request
			.patch("/asset/modify", {
				assetId,
				name,
				description: description || null
			})
			.then(() => {
				assetDetails.name = name;
				assetDetails.description = description;
				errorMessage = null;
			})
			.catch((e) => {
				errorMessage = e.message;
			})
			.finally(() => {
				disabled = false;
			});
	}
</script>

<style>
    p.err {
        color: red;
    }
    .wide-input {
        width: 100%;
    }
    .economy-section {
        margin-top: 5px;
        padding-top: 5px;
        border-top: 1px solid #ddd;
    }
    .basic-info-section {
        margin-top: 10px;
    }
    .update-product-btn {
        margin-top: 10px;
    }
    .offsale-time-container {
        margin-bottom: 10px;
    }
</style>

<svelte:head>
    <title>Update Product</title>
</svelte:head>

<Main>
    <div class="row">
        <div class="col-12">
            <h1>Update Product</h1>
            {#if errorMessage}
                <p class="err">{errorMessage}</p>
            {/if}
        </div>
        <div class="col-12">
            <label for="name">Asset ID</label>
        </div>
        <div class="col-4">
            <input
                type="text"
                class="form-control"
                id="asset_id"
                {disabled}
                bind:value={dirtyAssetId}
            />
        </div>
        <div class="col-4">
            <button
                class="btn btn-success"
                disabled={disabled}
                on:click={(e) => {
                    assetId = parseInt(dirtyAssetId, 10);
                }}>Search</button>
        </div>
        <div class="col-12">
            {#if assetId}
                <div class="row">
                    <div class="col-12">
                        <h2 class="mt-1 mb-2">Editing "{assetDetails.name || 'Asset'}"</h2>
                    </div>

                    {#if assetDetails.name !== undefined}
                        <div class="col-12 economy-section">
                            <h3>Economy</h3>
                            <div class="row">
                                <div class="col-2">
                                    <label for="name">R$ Price (Optional)</label>
                                    <input type="text" class="form-control" id="priceRobux" {disabled} value={assetDetails.priceRobux || ""} />
                                </div>
                                <div class="col-2">
                                    <label for="name">TX$ Price (Optional)</label>
                                    <input type="text" class="form-control" id="priceTickets" {disabled} value={assetDetails.priceTickets || ""} />
                                </div>
                                <div class="col-2 mt-4">
                                    <label for="is_for_sale">For Sale: </label>
                                    <input type="checkbox" class="form-check-input" id="is_for_sale" checked={assetDetails.isForSale || false} />
                                </div>
                            </div>
                            <div class="row mt-2">
                                <Permission p="MakeItemLimited">
                                    <div class="col-6">
                                        <label for="description">Limited Status</label>
                                        <select class="form-control" id="limited-status" value={assetDetails.isLimited ? "limited" : assetDetails.isLimitedUnique ? "limited_u" : "false"}>
                                            <option value="false">Not Limited</option>
                                            <option value="limited">Limited</option>
                                            <option value="limited_u">Limited Unique</option>
                                        </select>
                                    </div>
                                </Permission>
                                <div class="col-6">
                                    <label for="description">Max Copy Count (optional)</label>
                                    <input type="text" class="form-control" id="max-copies" value={assetDetails.serialCount || ""} />
                                </div>
								<div class="col-6 mt-1 offsale-time-container">
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
								<div class="col-2 mt-4">
									<label for="is_visible">Visible: </label>
									<input 
										type="checkbox" 
										class="form-check-input" 
										id="is_visible" 
										checked={assetDetails.isVisible ?? true} 
									/>
								</div>
                            </div>
                        </div>

                        <div class="col-12 update-product-btn">
                            <button
                                class="btn btn-success"
                                disabled={disabled}
                                on:click={(e) => {
                                    e.preventDefault();
                                    if (disabled) {
                                        return;
                                    }
									let offsaleOffsetValue = getElementById("offsale-offset-value").value;
									let offsaleOffsetUnit = getElementById("offsale-offset-unit").value;
									let offsaleDeadline = null;
									if (offsaleOffsetValue) {
										const offset = parseInt(offsaleOffsetValue);
										if (isNaN(offset) || offset <= 0) {
											errorMessage = "Offsale offset must be a positive number";
											return;
										}
										
										offsaleDeadline = new {
											value = offset,
											unit = offsaleOffsetUnit
										};
									}

                                    let isLimited = false;
                                    let isLimitedUnique = false;
                                    if (getElementById("limited-status")) {
                                        let limStatus = getElementById("limited-status").value;
                                        if (limStatus === "limited" || limStatus === "limited_u") {
                                            isLimited = true;
                                        }
                                        if (limStatus === "limited_u") {
                                            isLimitedUnique = true;
                                        }
                                    }
                                    let maxSerial = getElementById("max-copies").value || null;
                                    if (getElementById("max-copies")) {
                                        let maxSerial = getElementById("max-copies").value;
                                        if (Number.isSafeInteger(parseInt(maxSerial, 10))) {
                                            maxSerial = parseInt(maxSerial, 10);
                                        }else{
                                            maxSerial = null;
                                        }
                                    }
                                    let price = getElementById("priceRobux").value;
                                    if (Number.isSafeInteger(parseInt(price, 10))) {
                                        price = parseInt(price, 10);
                                    }else{
                                        price = null;
                                    }

                                    let priceTickets = getElementById('priceTickets').value;
                                    if (Number.isSafeInteger(parseInt(priceTickets, 10))) {
                                        priceTickets = parseInt(priceTickets, 10);
                                    }else{
                                        priceTickets = null;
                                    }
									
									const isVisible = getElementById("is_visible").checked;
                                    
                                    disabled = true;
									request
										.patch("/asset/product", {
											assetId,
											isForSale: getElementById("is_for_sale").checked,
											maxCopies: maxSerial,
											priceRobux: price,
											priceTickets: priceTickets,
											offsaleDeadline,
											isLimited,
											isLimitedUnique,
											isVisible
										})
                                        .then((d) => {
                                            window.location.href = `/catalog/${assetId}/--`;
                                        })
                                        .catch((e) => {
                                            console.log('[error]',e);
                                            errorMessage = e.message;
                                        })
                                        .finally(() => {
                                            disabled = false;
                                        });
                                }}>Update Product</button
                            >
                        </div>

                        <div class="col-12 basic-info-section">
                            <h3>Details</h3>
                            <div class="row">
                                <div class="col-12">
                                    <label for="asset-name">Name</label>
                                    <input 
                                        type="text" 
                                        class="form-control wide-input"
                                        id="asset-name" 
                                        bind:value={assetDetails.name}
                                    />
                                </div>
                                <div class="col-12 mt-1">
                                    <label for="asset-description">Description</label>
                                    <textarea 
                                        class="form-control wide-input" 
                                        id="asset-description" 
                                        rows="3"
                                        bind:value={assetDetails.description}
                                    ></textarea>
                                </div>
                                <div class="col-12 mt-1">
                                    <button
                                        class="btn btn-primary"
                                        disabled={disabled}
                                        on:click={updateAssetDetails}
                                    >
                                        Update Name/Description
                                    </button>
                                </div>
                            </div>
                        </div>
                    {/if}
                </div>
            {/if}
        </div>
		
		{#if assetId}
			<div class="col-12">
				<hr />
				<Permission p="GetSaleHistoryForAsset">
					<ProductHistory assetId={assetId}></ProductHistory>
				</Permission>
				<SaleHistory assetId={assetId}></SaleHistory>
			</div>
		{/if}
	</div>
</Main>