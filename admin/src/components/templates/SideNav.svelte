<script lang="ts">
	import { link } from "svelte-routing";
	import { HomeIcon, CopyIcon, UsersIcon, UserIcon, UserPlusIcon, GiftIcon, PaperclipIcon, ServerIcon, CpuIcon, MonitorIcon, ImageIcon, DollarSignIcon, BookOpenIcon, EyeIcon, PlusCircleIcon, EditIcon, FilePlusIcon, RefreshCcwIcon, UploadCloudIcon, CheckSquareIcon, TagIcon, SunriseIcon, FlagIcon, StarIcon, BookIcon, PhoneIcon, ActivityIcon, TabletIcon, ChevronDownIcon, ChevronRightIcon } from "svelte-feather-icons";

	import SavedPages, { addPage } from "../../stores/saved-pages";
	import PageEntry from "../saved-pages/PageEntry.svelte";
	import * as rank from "../../stores/rank";
	let savedPages: { url: string; title: string }[];
	SavedPages.subscribe((v) => (savedPages = v));

	let categories = {
		users: JSON.parse(sessionStorage.getItem('nav-users') || false),
		moderation: JSON.parse(sessionStorage.getItem('nav-moderation') || false),
		games: JSON.parse(sessionStorage.getItem('nav-games') || false),
		web: JSON.parse(sessionStorage.getItem('nav-web') || false),
		assets: JSON.parse(sessionStorage.getItem('nav-assets') || false),
		catalog: JSON.parse(sessionStorage.getItem('nav-catalog') || false)
	};

	const toggleCategory = (category: string) => {
		const newState = !categories[category];
		categories = { ...categories, [category]: newState };
		sessionStorage.setItem(`nav-${category}`, JSON.stringify(newState));
	};

	const navCategories = [
		{
			name: "DASHBOARD",
			link: "/admin/",
			icon: HomeIcon,
		},
		{
			id: "users",
			name: "USERS",
			// icon: UsersIcon,
			items: [
				{
					name: "Players",
					link: "/admin/players",
					icon: UsersIcon,
					permission: "GetUsersList",
				},
				{
					name: 'Groups',
					link: '/admin/groups',
					icon: ActivityIcon,
					permission: 'GetGroupManageInfo',
				},
				{
					name: "Create Player",
					link: "/admin/user/create",
					icon: UserPlusIcon,
					permission: "CreateUser",
				},
				{
					name: 'Forums',
					link: '/admin/forums',
					icon: BookIcon,
					permission: 'LockForumThread',
				},
			]
		},
		{
			id: "moderation",
			name: "MODERATION",
			// icon: CheckSquareIcon,
			items: [
				{
					name: "Asset Moderation",
					link: "/admin/asset/approval",
					icon: CheckSquareIcon,
					permission: "GetPendingModerationItems",
				},
				{
					name: 'Text Moderation',
					link: '/admin/text-posts',
					icon: PhoneIcon,
					permission: 'GetAllAssetComments',
				},
				{
					name: 'Reports',
					link: '/admin/reports',
					icon: StarIcon,
					permission: 'ManageReports',
				},
				{
					name: 'Possible Alts',
					link: '/admin/alts',
					icon: UserIcon,
					permission: 'GetUsersList',
				},
				{
					name: 'Transcripts',
					link: '/admin/transcripts',
					icon: PaperclipIcon,
					permission: 'GetAdminLogs',
				},
			]
		},
		{
			id: "games",
			name: "GAMES",
			// icon: MonitorIcon,
			items: [
				{
					name: "Running Game Servers",
					link: "/admin/game-servers/manage",
					icon: ServerIcon,
					permission: "ManageRunningGameServers",
				},
				{
					name: 'Game Server Status',
					link: '/admin/game-servers/status',
					icon: CpuIcon,
					permission: 'ManageRunningGameServers',
				},
				{
					name: 'Places',
					link: '/admin/places',
					icon: MonitorIcon,
					permission: 'ManageGames',
				},
			]
		},
		{
			id: "web",
			name: "WEB",
			// icon: BookOpenIcon,
			items: [
				{
					name: 'Game History',
					link: '/admin/game-history',
					icon: TabletIcon,
					permission: 'GetUsersInGame',
				},
				{
					name: "Logs",
					link: "/admin/logs",
					icon: BookOpenIcon,
					permission: "GetAdminLogs",
				},
				{
					name: "Feature Flags",
					link: "/admin/feature-flags",
					icon: FlagIcon,
					permission: "ManageFeatureFlags",
				},
			]
		},
		{
			id: "assets",
			name: "ASSETS",
			// icon: GiftIcon,
			items: [
				{
					name: "Update Asset Thumbnail",
					link: "/admin/asset/thumbnail",
					icon: ImageIcon,
					permission: "SetAssetModerationStatus",
				},
				{
					name: "Lottery",
					link: "/admin/lottery",
					icon: SunriseIcon,
					permission: "RunLottery",
				},
				{
					name: "Promocodes",
					link: "/admin/promocodes",
					icon: TagIcon,
					permission: "GiveUserItem",
				},
				{
					name: "Copy from ROBLOX",
					link: "/admin/asset/copy",
					icon: CopyIcon,
					permission: "CreateAssetCopiedFromRoblox",
				},
				{
					name: "Copy UGC from ROBLOX",
					link: "/admin/asset/ugc/copy",
					icon: CopyIcon,
					permission: "CreateAssetCopiedFromRoblox",
				},
				{
					name: "Copy Bundle from ROBLOX",
					link: "/admin/bundle/copy",
					icon: CopyIcon,
					permission: "CreateBundleCopiedFromRoblox",
				},
				{
					name: "Copy Animation Bundle from ROBLOX",
					link: "/admin/animation-bundle/copy",
					icon: CopyIcon,
					permission: "CreateBundleCopiedFromRoblox",
				}
			]
		}
	];

	let active: string = "/";
</script>

<div class="row">
	<nav id="sidebarMenu" class="col-md-3 col-lg-2 d-md-block bg-dark text-white sidebar collapse">
		<div class="position-sticky pt-3">
			<h6 class="sidebar-heading d-flex justify-content-between align-items-center px-3 mt-0 mb-1 text-muted">
				<span>Saved pages</span>
				<a
					class="link-secondary"
					href="#!"
					on:click={(e) => {
						e.preventDefault();
						let title = document.title;
						if (title === "Admin" || !title) {
							title = "Untitled Page";
						}
						let url = location.pathname + (location.search || "");
						addPage(title, url);
					}}
				>
					<PlusCircleIcon />
				</a>
			</h6>
			<ul class="nav flex-column mb-2">
				{#if savedPages.length === 0}
					<ul class="nav flex-column mb-0">
						<li class="nav-item">
							<p class="pr-4 pl-4">You do not have any saved pages. Click the <PlusCircleIcon /> icon to save a page.</p>
						</li>
					</ul>
				{:else}
					{#each savedPages as p}
						<PageEntry title={p.title} url={p.url} />
					{/each}
				{/if}
			</ul>
			<ul class="nav flex-column">
				{#each navCategories as category}
					{#if !category.id || (category.items && category.items.some(item => !item.permission || rank.hasPermission(item.permission)))}
						{#if category.link}
							<li class="nav-item">
								<a use:link class={`nav-link${category.link === active ? " active" : ""}`} href={category.link}>
									<svelte:component this={category.icon} />
									{category.name}
								</a>
							</li>
						{:else}
							<li class="nav-item">
								<a 
									class="nav-link d-flex justify-content-between align-items-center" 
									href="#!" 
									on:click|preventDefault={() => toggleCategory(category.id)}
								>
									<span>
										<svelte:component this={category.icon} />
										{category.name}
									</span>
									<svelte:component this={categories[category.id] ? ChevronDownIcon : ChevronRightIcon} size="16" />
								</a>
								{#if categories[category.id]}
									<ul class="nav flex-column ml-4 pl-3" style="border-left: 1px solid rgba(255,255,255,0.1);">
										{#each category.items as item}
											{#if !item.permission || rank.hasPermission(item.permission)}
												<li class="nav-item">
													<a use:link class={`nav-link${item.link === active ? " active" : ""}`} href={item.link}>
														<svelte:component this={item.icon} />
														{item.name}
													</a>
												</li>
											{/if}
										{/each}
									</ul>
								{/if}
							</li>
						{/if}
					{/if}
				{/each}
				
				{#if rank.hasPermission("CreateAsset") || rank.hasPermission("SetAssetProduct") || rank.hasPermission("MigrateAssetFromRoblox") || rank.hasPermission("CreateAssetVersion") || rank.hasPermission("RequestAssetReRender") || rank.hasPermission('CreateBundleCopiedFromRoblox') || rank.hasPermission('CreateAssetCopiedFromRoblox') || rank.hasPermission("GiveUserItem")}
					<li class="nav-item">
						<a 
							class="nav-link d-flex justify-content-between align-items-center" 
							href="#!" 
							on:click|preventDefault={() => toggleCategory('catalog')}
						>
							<span>CATALOG</span>
							<svelte:component this={categories.catalog ? ChevronDownIcon : ChevronRightIcon} size="16" />
						</a>
						{#if categories.catalog}
							<ul class="nav flex-column ml-4 pl-3" style="border-left: 1px solid rgba(255,255,255,0.1);">
								{#if rank.hasPermission("CreateAsset")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/asset/create"><FilePlusIcon /> Create Item</a>
									</li>
								{/if}
								{#if rank.hasPermission("CreateAsset")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/asset/create/custom"><FilePlusIcon /> Create Custom Item</a>
									</li>
								{/if}
								{#if rank.hasPermission("SetAssetProduct")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/product/update"><EditIcon /> Update Item Product</a>
									</li>
								{/if}
								{#if rank.hasPermission("CreateAssetVersion")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/asset/version/create"><UploadCloudIcon /> Update Item RBXM</a>
									</li>
								{/if}
								{#if rank.hasPermission("GiveUserItem")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/asset/track"><EyeIcon /> Get Asset Owners</a>
									</li>
								{/if}
								{#if rank.hasPermission("RequestAssetReRender")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/asset/re-render"><RefreshCcwIcon /> Re-Render</a>
									</li>
								{/if}
								{#if rank.hasPermission("SetAssetProduct")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/asset/rap"><DollarSignIcon /> Set Item RAP</a>
									</li>
								{/if}
								{#if rank.hasPermission("GiveUserItem")}
									<li class="nav-item">
										<a use:link class="nav-link" href="/admin/gift/open"><GiftIcon /> Open Gift</a>
									</li>
								{/if}
							</ul>
						{/if}
					</li>
				{/if}
				
				<li class="nav-item mt-2 d-md-none d-block">
					<a class="nav-link" href="/home">Back to BubbaBlox</a>
				</li>
			</ul>
		</div>
	</nav>
</div>

<style>
	nav#sidebarMenu.sidebar {
		top: 0 !important;
	}

	.sidebar-heading span {
		color: white !important;
	}
	
	.nav-link {
		padding-left: 1rem !important;
		padding-right: 1rem !important;
	}
	
	.nav-link.active {
		background-color: rgba(255, 255, 255, 0.1);
	}
	
	.ml-4 {
		margin-left: 1.5rem !important;
	}
	
	.pl-3 {
		padding-left: 1rem !important;
	}
</style>