(() => {
	// Defensive Chart wrapper: if Chart is called with a null/undefined item,
	// skip creation and return a minimal stub so other code can call .destroy().
	try {
		const OriginalChart = window.Chart;
		if (OriginalChart && typeof OriginalChart === 'function') {
			window.Chart = function(item, config) {
				if (!item) {
					console.warn('Chart creation skipped: canvas/context is null', config && config.type);
					return { destroy: function() {} };
				}
				// If passed a context (CanvasRenderingContext2D), accept it
				return new OriginalChart(item, config);
			};
			// preserve prototype for instanceof checks
			try { window.Chart.prototype = OriginalChart.prototype; } catch {}
		}
	} catch (e) {
		console.warn('Failed to install Chart guard', e);
	}
	const body = document.body;
	const collapseBtn = document.getElementById("btnCollapseSidebar");
	const openBtn = document.getElementById("btnOpenSidebar");
	const loginForm = document.getElementById("loginForm");
	const loginUser = document.getElementById("loginUser");
	const loginPassword = document.getElementById("loginPassword");
	const loginUserError = document.getElementById("loginUserError");
	const loginPasswordError = document.getElementById("loginPasswordError");
	const loginGeneralError = document.getElementById("loginGeneralError");
	const loginLoading = document.getElementById("loginLoading");
	const loginBtnText = document.querySelector(".login-btn-text");
	const loginSection = document.getElementById("loginSection");
	const forgotPasswordSection = document.getElementById("forgotPasswordSection");
	const btnShowForgotPassword = document.getElementById("btnShowForgotPassword");
	const btnBackToLogin = document.getElementById("btnBackToLogin");
	const forgotStep1 = document.getElementById("forgotStep1");
	const forgotStep2 = document.getElementById("forgotStep2");
	const forgotStep3 = document.getElementById("forgotStep3");
	const forgotGeneralResult = document.getElementById("forgotGeneralResult");
	const forgotEmailForm = document.getElementById("forgotEmailForm");
	const forgotCodeForm = document.getElementById("forgotCodeForm");
	const forgotResetForm = document.getElementById("forgotResetForm");
	const forgotEmail = document.getElementById("forgotEmail");
	const forgotVerifyCode = document.getElementById("forgotVerifyCode");
	const forgotNewPassword = document.getElementById("forgotNewPassword");
	const forgotConfirmPassword = document.getElementById("forgotConfirmPassword");
	const forgotEmailError = document.getElementById("forgotEmailError");
	const forgotCodeError = document.getElementById("forgotCodeError");
	const forgotNewPasswordError = document.getElementById("forgotNewPasswordError");
	const forgotConfirmPasswordError = document.getElementById("forgotConfirmPasswordError");
	const notificationWrap = document.getElementById("notificationBell");
	const notificationBadge = document.getElementById("notificationUnreadBadge");
	const notificationList = document.getElementById("notificationListPreview");
	const notificationTabs = Array.from(document.querySelectorAll(".notification-tab[data-tab]"));
	const markAllBtn = document.getElementById("btnMarkAllNotificationsRead");
	const notificationTrigger = notificationWrap?.querySelector(".notification-trigger");
	const mainContent = document.querySelector(".main-content");
	const aiSectionNav = document.getElementById("aiSectionNav");
	const aiLinkSelector = 'a[data-ai-link="true"]';
	const topbarSearchInput = document.querySelector(".topbar .search-box input");

	const notificationState = {
		tab: "all",
		role: (notificationWrap?.dataset.roleKey || "admin").trim().toLowerCase(),
		unreadCount: 0,
		latestKey: "",
		pollingMs: 20000,
		isLoading: false
	};

	function normalizeSearchText(value) {
		return (value || "")
			.toString()
			.trim()
			.toLowerCase()
			.normalize("NFD")
			.replace(/[\u0300-\u036f]/g, "");
	}

	function setupTopbarSearch() {
		if (!topbarSearchInput) return;

		const sidebarLinks = Array.from(document.querySelectorAll(".sidebar-nav .nav-item"));
		if (!sidebarLinks.length) return;

		topbarSearchInput.addEventListener("keydown", (event) => {
			if (event.key !== "Enter") return;
			event.preventDefault();

			const keyword = normalizeSearchText(topbarSearchInput.value);
			if (!keyword) return;

			const matchedLink = sidebarLinks.find((link) =>
				normalizeSearchText(link.textContent).includes(keyword)
			);

			if (matchedLink && matchedLink.href) {
				window.location.href = matchedLink.href;
				return;
			}

			topbarSearchInput.classList.add("is-invalid");
			window.setTimeout(() => topbarSearchInput.classList.remove("is-invalid"), 1200);
		});
	}

	const notificationTypeLabel = {
		task: "Task",
		kpi: "KPI",
		alert: "Canh bao",
		ai: "AI",
		system: "He thong"
	};

	const loadingDelayProfiles = Object.freeze({
		default: 150,
		export: 250,
		save: 120,
		filter: 120,
		assign: 120,
		upload: 120,
		delete: 150,
		reload: 150,
		heavy: 150
	});

	let loadingMinDelayMs = loadingDelayProfiles.default;

	function resolveButtonTarget(buttonOrSelector) {
		if (!buttonOrSelector) return null;
		if (typeof buttonOrSelector === "string") {
			return document.querySelector(buttonOrSelector);
		}
		return buttonOrSelector instanceof HTMLElement ? buttonOrSelector : null;
	}

	function setButtonLoadingState(buttonOrSelector, isLoading) {
		const button = resolveButtonTarget(buttonOrSelector);
		if (!button) return;

		if (isLoading) {
			if (!button.dataset.lvWasDisabled) {
				button.dataset.lvWasDisabled = button.disabled ? "1" : "0";
			}
			button.disabled = true;
			button.setAttribute("aria-busy", "true");
			button.classList.add("is-loading");
			return;
		}

		const wasDisabled = button.dataset.lvWasDisabled === "1";
		delete button.dataset.lvWasDisabled;
		button.disabled = wasDisabled;
		button.removeAttribute("aria-busy");
		button.classList.remove("is-loading");
	}

	function resolveProfileDelay(profileName) {
		if (typeof profileName !== "string") return null;
		const key = profileName.trim().toLowerCase();
		if (!key) return null;

		const profileDelay = Number(loadingDelayProfiles[key]);
		return Number.isFinite(profileDelay) ? Math.max(0, profileDelay) : null;
	}

	function resolveLoadingDelay(options = {}) {
		const configuredDelay = Number(options?.minDelayMs);
		if (Number.isFinite(configuredDelay)) {
			return Math.max(0, configuredDelay);
		}

		const profileDelay = resolveProfileDelay(options?.profile);
		if (profileDelay !== null) {
			return profileDelay;
		}

		return loadingMinDelayMs;
	}

	async function runWithButtonLoading(buttonOrSelector, action, options = {}) {
		const button = resolveButtonTarget(buttonOrSelector);
		const minDelayMs = resolveLoadingDelay(options);

		let shouldClearLoading = false;
		let loadingTimerId = null;

		const startLoading = () => {
			shouldClearLoading = true;
			setButtonLoadingState(button, true);
		};

		if (minDelayMs > 0) {
			loadingTimerId = window.setTimeout(startLoading, minDelayMs);
		} else {
			startLoading();
		}

		try {
			return await Promise.resolve(action ? action() : undefined);
		} finally {
			if (loadingTimerId !== null) {
				window.clearTimeout(loadingTimerId);
			}

			if (shouldClearLoading) {
				setButtonLoadingState(button, false);
			}
		}
	}

	function bindButtonLoading(buttonOrSelector, handler, eventName = "click", options = {}) {
		const button = resolveButtonTarget(buttonOrSelector);
		if (!button || typeof handler !== "function") return;

		let resolvedEventName = eventName;
		let resolvedOptions = options;

		if (typeof eventName === "object" && eventName !== null) {
			resolvedOptions = eventName;
			resolvedEventName = "click";
		}

		button.addEventListener(resolvedEventName, async (event) => {
			await runWithButtonLoading(button, () => handler(event), resolvedOptions);
		});
	}

	function isButtonLoading(button) {
		return button.classList.contains("is-loading") || button.getAttribute("aria-busy") === "true";
	}

	async function runSingleLoadingSmokeCase(button, name, actionDurationMs, options, expected) {
		const timeline = [];
		const baseDisabled = !!button.disabled;

		const sample = (tag) => {
			timeline.push({
				tag,
				t: performance.now(),
				loading: isButtonLoading(button),
				disabled: !!button.disabled,
				ariaBusy: button.getAttribute("aria-busy") === "true"
			});
		};

		const observer = new MutationObserver(() => sample("mutation"));
		observer.observe(button, {
			attributes: true,
			attributeFilter: ["class", "aria-busy", "disabled"]
		});

		sample("before");
		const startedAt = performance.now();

		await runWithButtonLoading(
			button,
			() => new Promise((resolve) => window.setTimeout(resolve, actionDurationMs)),
			options
		);

		const endedAt = performance.now();
		sample("after");
		observer.disconnect();

		const showedLoading = timeline.some((x) => x.loading);
		const firstLoadingPoint = timeline.find((x) => x.loading);
		const firstLoadingAtMs = firstLoadingPoint ? Math.max(0, Math.round(firstLoadingPoint.t - startedAt)) : null;
		const finalStateCleared = !isButtonLoading(button);
		const restoredDisabledState = !!button.disabled === baseDisabled;
		const hadDisabledDuringLoading = timeline.some((x) => x.loading && x.disabled);

		const expectedShow = expected?.showLoading;
		const expectationMatched = typeof expectedShow === "boolean"
			? expectedShow === showedLoading
			: true;

		const passed = expectationMatched && finalStateCleared && restoredDisabledState;

		return {
			name,
			pass: passed,
			expectedShowLoading: expectedShow,
			showedLoading,
			firstLoadingAtMs,
			actionDurationMs,
			totalDurationMs: Math.round(endedAt - startedAt),
			finalStateCleared,
			hadDisabledDuringLoading,
			restoredDisabledState,
			options
		};
	}

	async function runLoadingSmokeTest(buttonOrSelector, config = {}) {
		const targetButton = resolveButtonTarget(buttonOrSelector);
		const createdButton = !targetButton;
		const button = targetButton || document.createElement("button");

		if (createdButton) {
			button.type = "button";
			button.textContent = "UiButtonLoading Smoke";
			button.style.position = "fixed";
			button.style.left = "-9999px";
			button.style.top = "-9999px";
			document.body.appendChild(button);
		}

		const defaultCases = [
			{
				name: "quick-under-threshold",
				actionDurationMs: 80,
				options: { profile: "save" },
				expected: { showLoading: false }
			},
			{
				name: "medium-over-threshold",
				actionDurationMs: 200,
				options: { profile: "save" },
				expected: { showLoading: true }
			},
			{
				name: "slow-long-operation",
				actionDurationMs: 1200,
				options: { profile: "heavy" },
				expected: { showLoading: true }
			}
		];

		const testCases = Array.isArray(config?.cases) && config.cases.length
			? config.cases
			: defaultCases;

		const caseResults = [];
		for (const testCase of testCases) {
			caseResults.push(await runSingleLoadingSmokeCase(
				button,
				testCase.name,
				Number(testCase.actionDurationMs || 0),
				testCase.options || {},
				testCase.expected || {}
			));
		}

		if (createdButton && button.parentNode) {
			button.parentNode.removeChild(button);
		}

		const passedCount = caseResults.filter((x) => x.pass).length;
		const summary = {
			pass: passedCount === caseResults.length,
			passedCount,
			totalCount: caseResults.length,
			cases: caseResults
		};

		if (config?.log !== false) {
			console.group("UiButtonLoading smoke test");
			console.table(caseResults.map((x) => ({
				name: x.name,
				pass: x.pass,
				expectedShowLoading: x.expectedShowLoading,
				showedLoading: x.showedLoading,
				firstLoadingAtMs: x.firstLoadingAtMs,
				actionDurationMs: x.actionDurationMs,
				totalDurationMs: x.totalDurationMs,
				finalStateCleared: x.finalStateCleared,
				hadDisabledDuringLoading: x.hadDisabledDuringLoading,
				restoredDisabledState: x.restoredDisabledState
			})));
			console.log(`Result: ${passedCount}/${caseResults.length} passed`);
			console.groupEnd();
		}

		return summary;
	}

	window.UiButtonLoading = {
		set: (buttonOrSelector) => setButtonLoadingState(buttonOrSelector, true),
		clear: (buttonOrSelector) => setButtonLoadingState(buttonOrSelector, false),
		run: runWithButtonLoading,
		bind: bindButtonLoading,
		smokeTest: runLoadingSmokeTest,
		profiles: loadingDelayProfiles,
		getProfileDelay: (profileName) => resolveProfileDelay(profileName),
		profile: (profileName, overrides = {}) => ({
			...(overrides && typeof overrides === "object" ? overrides : {}),
			profile: profileName
		}),
		setMinDelay: (ms) => {
			const parsed = Number(ms);
			loadingMinDelayMs = Number.isFinite(parsed) ? Math.max(0, parsed) : loadingMinDelayMs;
			return loadingMinDelayMs;
		},
		getMinDelay: () => loadingMinDelayMs
	};

	function ensureToastContainer() {
		let container = document.getElementById("appToastContainer");
		if (container) return container;

		container = document.createElement("div");
		container.id = "appToastContainer";
		container.className = "toast-container position-fixed top-0 end-0 p-3";
		container.style.zIndex = "1080";
		document.body.appendChild(container);
		return container;
	}

	function toastClassByType(type) {
		switch ((type || "info").toLowerCase()) {
			case "success":
				return "text-bg-success";
			case "error":
			case "danger":
				return "text-bg-danger";
			case "warning":
				return "text-bg-warning";
			default:
				return "text-bg-primary";
		}
	}

	function showToast(options = {}) {
		const message = String(options.message || "").trim();
		if (!message) return;

		const type = options.type || "info";
		const delay = Number.isFinite(Number(options.delay)) ? Math.max(1200, Number(options.delay)) : 2600;
		const container = ensureToastContainer();

		const toastNode = document.createElement("div");
		toastNode.className = `toast align-items-center border-0 ${toastClassByType(type)}`;
		toastNode.setAttribute("role", "status");
		toastNode.setAttribute("aria-live", "polite");
		toastNode.setAttribute("aria-atomic", "true");

		toastNode.innerHTML = `
			<div class="d-flex">
				<div class="toast-body">${escapeHtml(message)}</div>
				<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
			</div>
		`;

		container.appendChild(toastNode);

		if (window.bootstrap && window.bootstrap.Toast) {
			const instance = new window.bootstrap.Toast(toastNode, { delay, autohide: true });
			toastNode.addEventListener("hidden.bs.toast", () => toastNode.remove(), { once: true });
			instance.show();
			return;
		}

		// Fallback when Bootstrap JS is unavailable
		window.setTimeout(() => toastNode.remove(), delay);
	}

	window.UiToast = {
		show: showToast,
		success: (message, delay) => showToast({ message, type: "success", delay }),
		error: (message, delay) => showToast({ message, type: "error", delay }),
		info: (message, delay) => showToast({ message, type: "info", delay }),
		warning: (message, delay) => showToast({ message, type: "warning", delay })
	};

	if (collapseBtn) {
		collapseBtn.addEventListener("click", () => {
			body.classList.toggle("sidebar-collapsed");
		});
	}

	if (openBtn) {
		openBtn.addEventListener("click", () => {
			body.classList.toggle("sidebar-open");
		});
	}

	document.addEventListener("click", (event) => {
		if (!body.classList.contains("sidebar-open")) {
			return;
		}

		const sidebar = document.getElementById("sidebar");
		if (!sidebar) {
			return;
		}

		const clickedInsideSidebar = sidebar.contains(event.target);
		const clickedOpenButton = openBtn && openBtn.contains(event.target);

		if (!clickedInsideSidebar && !clickedOpenButton) {
			body.classList.remove("sidebar-open");
		}
	});

	function isAiPath(url) {
		try {
			const parsed = new URL(url, window.location.origin);
			return /\/portal\/ai/i.test(parsed.pathname);
		} catch {
			return false;
		}
	}

	function updateAiNavActive(url) {
		if (!aiSectionNav) return;
		const parsed = new URL(url, window.location.origin);
		const activePath = parsed.pathname.toLowerCase();
		aiSectionNav.classList.remove("d-none");
		aiSectionNav.querySelectorAll(aiLinkSelector).forEach((link) => {
			const linkPath = new URL(link.getAttribute("href") || "", window.location.origin).pathname.toLowerCase();
			link.classList.toggle("active", linkPath === activePath);
		});
	}

	function executeInlineScripts(doc) {
		const inlineScripts = Array.from(doc.querySelectorAll("script:not([src])"));
		inlineScripts.forEach((scriptNode) => {
			const script = document.createElement("script");
			// Run each inline script in an isolated scope to avoid "Identifier has
			// already been declared" when navigating AI pages via AJAX.
			// Use string concatenation (not template literals) to avoid breaking
			// scripts that contain backticks or `${...}`.
			script.textContent = "(function(){\n" + (scriptNode.textContent || "") + "\n})();";
			document.body.appendChild(script);
			script.remove();
		});
	}

	async function loadAiRoute(url, pushHistory = true) {
		const currentMainContent = document.querySelector(".main-content") || mainContent;
		if (!currentMainContent) {
			window.location.href = url;
			return;
		}

		try {
			const response = await fetch(url, {
				headers: {
					"X-Requested-With": "XMLHttpRequest"
				}
			});

			if (!response.ok) {
				window.location.href = url;
				return;
			}

			const html = await response.text();
			const doc = new DOMParser().parseFromString(html, "text/html");
			const nextMainContent = doc.querySelector(".main-content");

			if (!nextMainContent) {
				window.location.href = url;
				return;
			}

			currentMainContent.innerHTML = nextMainContent.innerHTML;
			document.title = doc.title || document.title;
			updateAiNavActive(url);
			executeInlineScripts(doc);

			if (pushHistory) {
				history.pushState({ aiAjax: true, url }, "", url);
			}
			window.scrollTo({ top: 0, behavior: "smooth" });
		} catch (error) {
			window.location.href = url;
		}
	}

	document.addEventListener("click", (event) => {
		const link = event.target.closest(aiLinkSelector);
		if (!link) return;
		if (!isAiPath(link.href)) return;

		event.preventDefault();
		loadAiRoute(link.href, true);
	});

	window.addEventListener("popstate", () => {
		if (isAiPath(window.location.href)) {
			loadAiRoute(window.location.href, false);
		} else if (aiSectionNav) {
			aiSectionNav.classList.add("d-none");
			window.location.reload();
		}
	});

	if (isAiPath(window.location.href)) {
		updateAiNavActive(window.location.href);
	}

	function looksLikeEmail(value) {
		return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
	}

	function isEmailInput(value) {
		return value.includes("@");
	}

	function markForgotStep(activeStep) {
		const steps = [forgotStep1, forgotStep2, forgotStep3];
		steps.forEach((step, index) => {
			if (!step) return;
			const stepNo = index + 1;
			step.classList.toggle("active", stepNo === activeStep);
			step.classList.toggle("done", stepNo < activeStep);
		});
	}

	function showForgotMessage(message, type) {
		if (!forgotGeneralResult) return;
		forgotGeneralResult.className = `alert alert-${type}`;
		forgotGeneralResult.textContent = message;
		forgotGeneralResult.classList.remove("d-none");
	}

	function clearForgotMessage() {
		if (!forgotGeneralResult) return;
		forgotGeneralResult.classList.add("d-none");
		forgotGeneralResult.textContent = "";
	}

	function setForgotView(step) {
		forgotEmailForm?.classList.toggle("d-none", step !== 1);
		forgotCodeForm?.classList.toggle("d-none", step !== 2);
		forgotResetForm?.classList.toggle("d-none", step !== 3);
		markForgotStep(step);
	}

	function resetForgotFlow() {
		clearForgotMessage();
		forgotEmailError?.classList.add("d-none");
		forgotCodeError?.classList.add("d-none");
		forgotNewPasswordError?.classList.add("d-none");
		forgotConfirmPasswordError?.classList.add("d-none");
		if (forgotEmail) forgotEmail.value = "";
		if (forgotVerifyCode) forgotVerifyCode.value = "";
		if (forgotNewPassword) forgotNewPassword.value = "";
		if (forgotConfirmPassword) forgotConfirmPassword.value = "";
		setForgotView(1);
	}

	function showLoginView() {
		loginSection?.classList.remove("d-none");
		forgotPasswordSection?.classList.add("d-none");
		loginGeneralError?.classList.add("d-none");
	}

	function showForgotView() {
		loginSection?.classList.add("d-none");
		forgotPasswordSection?.classList.remove("d-none");
		resetForgotFlow();
	}

	function validateLoginUser() {
		const value = loginUser?.value.trim() || "";
		if (!value) {
			loginUserError.textContent = "Vui lòng nhập tài khoản hoặc email.";
			loginUserError?.classList.remove("d-none");
			return false;
		}

		if (isEmailInput(value) && !looksLikeEmail(value)) {
			loginUserError.textContent = "Email không đúng định dạng.";
			loginUserError?.classList.remove("d-none");
			return false;
		}

		loginUserError?.classList.add("d-none");
		return true;
	}

	function validateLoginPassword() {
		const isValid = (loginPassword?.value.trim().length || 0) > 0;
		loginPasswordError?.classList.toggle("d-none", isValid);
		return isValid;
	}

	if (loginForm && loginUser && loginPassword) {
		const validateUser = () => {
			return validateLoginUser();
		};

		const validatePassword = () => {
			return validateLoginPassword();
		};

		loginUser.addEventListener("input", validateUser);
		loginPassword.addEventListener("input", validatePassword);

		loginForm.addEventListener("submit", (event) => {
			loginGeneralError?.classList.add("d-none");

			const userOk = validateUser();
			const passOk = validatePassword();

			if (!userOk || !passOk) {
				event.preventDefault();
				return;
			}

			if (loginLoading) loginLoading.classList.remove("d-none");
			if (loginBtnText) loginBtnText.textContent = "Dang xac thuc...";
		});
	}

	document.querySelectorAll(".password-toggle[data-target]").forEach((toggleBtn) => {
		toggleBtn.addEventListener("click", () => {
			const targetId = toggleBtn.getAttribute("data-target");
			if (!targetId) return;
			const targetInput = document.getElementById(targetId);
			if (!targetInput) return;

			const willShow = targetInput.type === "password";
			targetInput.type = willShow ? "text" : "password";
			toggleBtn.textContent = willShow ? "Ẩn" : "Hiện";
			toggleBtn.setAttribute("aria-label", willShow ? "Ẩn mật khẩu" : "Hiện mật khẩu");
		});
	});

	btnShowForgotPassword?.addEventListener("click", showForgotView);
	btnBackToLogin?.addEventListener("click", showLoginView);

	forgotEmailForm?.addEventListener("submit", (event) => {
		event.preventDefault();
		clearForgotMessage();

		const emailValue = forgotEmail?.value.trim() || "";
		const isValidEmail = looksLikeEmail(emailValue);
		forgotEmailError?.classList.toggle("d-none", isValidEmail);

		if (!isValidEmail) {
			return;
		}

		showForgotMessage("Mã xác nhận đã được gửi đến email của bạn.", "success");
		setForgotView(2);
	});

	forgotCodeForm?.addEventListener("submit", (event) => {
		event.preventDefault();
		clearForgotMessage();

		const code = (forgotVerifyCode?.value || "").trim();
		const isCodeValid = /^\d{6}$/.test(code);
		forgotCodeError?.classList.toggle("d-none", isCodeValid);

		if (!isCodeValid) {
			showForgotMessage("Mã xác nhận không hợp lệ hoặc đã hết hạn.", "danger");
			return;
		}

		showForgotMessage("Xác nhận mã thành công. Vui lòng đặt lại mật khẩu mới.", "success");
		setForgotView(3);
	});

	forgotResetForm?.addEventListener("submit", (event) => {
		event.preventDefault();
		clearForgotMessage();

		const newPassword = forgotNewPassword?.value || "";
		const confirmPassword = forgotConfirmPassword?.value || "";

		const newPasswordValid = newPassword.trim().length >= 6;
		forgotNewPasswordError?.classList.toggle("d-none", newPasswordValid);

		const confirmValid = newPassword === confirmPassword && confirmPassword.length > 0;
		forgotConfirmPasswordError?.classList.toggle("d-none", confirmValid);

		if (!newPasswordValid || !confirmValid) {
			return;
		}

		showForgotMessage("Đặt lại mật khẩu thành công. Hệ thống sẽ chuyển về trang đăng nhập.", "success");

		setTimeout(() => {
			showLoginView();
			resetForgotFlow();
		}, 900);
	});

	function escapeHtml(value) {
		return String(value ?? "")
			.replace(/&/g, "&amp;")
			.replace(/</g, "&lt;")
			.replace(/>/g, "&gt;")
			.replace(/"/g, "&quot;")
			.replace(/'/g, "&#39;");
	}

	async function fetchJson(url, options) {
		const response = await fetch(url, options);
		if (!response.ok) {
			throw new Error(`HTTP ${response.status}`);
		}
		const payload = await response.json();
		if (!payload.success) {
			throw new Error(payload.message || "Co loi xay ra");
		}
		return payload.data;
	}

	function getRelativeTime(isoString) {
		if (!isoString) return "Vua xong";
		const date = new Date(isoString);
		if (Number.isNaN(date.getTime())) return "Vua xong";

		const now = new Date();
		const diffMs = now.getTime() - date.getTime();
		const diffMin = Math.max(1, Math.floor(diffMs / 60000));
		if (diffMin < 60) return `${diffMin} phut truoc`;

		const diffHour = Math.floor(diffMin / 60);
		if (diffHour < 24) return `${diffHour} gio truoc`;

		const diffDay = Math.floor(diffHour / 24);
		if (diffDay === 1) return "Hom qua";
		if (diffDay < 7) return `${diffDay} ngay truoc`;

		return date.toLocaleDateString("vi-VN", {
			day: "2-digit",
			month: "2-digit",
			year: "numeric"
		});
	}

	function getGroupLabel(isoString) {
		if (!isoString) return "Gan day";
		const date = new Date(isoString);
		if (Number.isNaN(date.getTime())) return "Gan day";

		const now = new Date();
		const startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
		const startYesterday = new Date(startToday);
		startYesterday.setDate(startYesterday.getDate() - 1);

		if (date >= startToday) return "hôm nay";
		if (date >= startYesterday) return "Hom qua";
		return "Cu hon";
	}

	function updateBadge(unreadCount) {
		if (!notificationBadge) return;
		if (unreadCount > 0) {
			notificationBadge.classList.remove("d-none");
			notificationBadge.textContent = unreadCount > 9 ? "9+" : String(unreadCount);
		} else {
			notificationBadge.classList.add("d-none");
			notificationBadge.textContent = "0";
		}
	}

	function triggerBellRing(previousUnread, nextUnread) {
		if (!notificationTrigger) return;
		if (nextUnread <= previousUnread || nextUnread <= 0) return;
		notificationTrigger.classList.remove("bell-ring");
		// Force reflow so animation replays each time there is a new notification.
		void notificationTrigger.offsetWidth;
		notificationTrigger.classList.add("bell-ring");
	}

	function renderPreview(items) {
		if (!notificationList) return;

		if (!items.length) {
			notificationList.innerHTML = '<div class="notification-empty">Không có thong bao.</div>';
			return;
		}

		let html = "";
		let lastGroup = "";
		items.forEach((item) => {
			const group = getGroupLabel(item.thoiGian);
			if (group !== lastGroup) {
				html += `<div class="notification-group-label">${escapeHtml(group)}</div>`;
				lastGroup = group;
			}

			const unreadClass = item.isUnread ? "unread" : "read";
			const typeClass = item.mau || "type-system";
			const typeName = item.loaiHienThi || notificationTypeLabel[item.loai] || "He thong";

			html += `
				<article class="notification-item ${unreadClass}" data-id="${item.maThongBao}" data-url="${escapeHtml(item.detailUrl || "#")}">
					<div class="notification-item-icon ${escapeHtml(typeClass)}">
						<i class="${escapeHtml(item.icon || "bi bi-gear")}"></i>
					</div>
					<div class="notification-item-body">
						<div class="notification-item-content">${escapeHtml(item.noiDung)}</div>
						<div class="notification-item-meta">
							<span>${escapeHtml(typeName)}</span>
							<span>${escapeHtml(getRelativeTime(item.thoiGian))}</span>
						</div>
					</div>
					<div class="notification-item-actions">
						<button type="button" class="btn btn-sm btn-light" data-action="mark" ${item.isUnread ? "" : "disabled"} aria-label="Danh dau đã đọc">
							<i class="bi bi-check2"></i>
						</button>
						<button type="button" class="btn btn-sm btn-outline-danger" data-action="delete" aria-label="Xóa thong bao">
							<i class="bi bi-trash"></i>
						</button>
					</div>
				</article>
			`;
		});

		notificationList.innerHTML = html;
	}

	async function loadNotificationSummary() {
		if (!notificationWrap) return;

		try {
			const summary = await fetchJson(`/thongbao/summary?role=${encodeURIComponent(notificationState.role)}`);
			const previousUnread = notificationState.unreadCount;
			notificationState.unreadCount = Number(summary.unreadCount || 0);
			notificationState.pollingMs = Number(summary.pollingIntervalMs || 20000);
			notificationState.latestKey = String(summary.latestTime || "");

			updateBadge(notificationState.unreadCount);
			triggerBellRing(previousUnread, notificationState.unreadCount);
		} catch (error) {
			console.warn("Không thể tai summary thong bao", error);
		}
	}

	async function loadNotificationPreview() {
		if (!notificationList || notificationState.isLoading) return;

		notificationState.isLoading = true;
		notificationList.innerHTML = '<div class="notification-empty">Đang tải thong bao...</div>';

		try {
			const params = new URLSearchParams({
				role: notificationState.role,
				tab: notificationState.tab,
				page: "1",
				size: "8"
			});

			const payload = await fetchJson(`/thongbao?${params.toString()}`);
			const items = payload.page?.items || [];
			notificationState.unreadCount = Number(payload.unreadCount || 0);
			updateBadge(notificationState.unreadCount);
			renderPreview(items);
		} catch (error) {
			notificationList.innerHTML = '<div class="notification-empty text-danger">Không tai duoc thong bao.</div>';
			console.warn("Không thể tai danh sach thong bao", error);
		} finally {
			notificationState.isLoading = false;
		}
	}

	async function markAllAsRead() {
		await fetchJson("/thongbao/mark-all-read", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ role: notificationState.role })
		});
	}

	async function markOneAsRead(maThongBao) {
		await fetchJson(`/thongbao/${maThongBao}/mark-read`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ role: notificationState.role })
		});
	}

	async function deleteOne(maThongBao) {
		await fetchJson(`/thongbao/${maThongBao}?role=${encodeURIComponent(notificationState.role)}`, {
			method: "DELETE"
		});
	}

	if (notificationWrap && notificationList) {
		notificationTabs.forEach((tabBtn) => {
			tabBtn.addEventListener("click", async () => {
				notificationTabs.forEach((x) => x.classList.remove("active"));
				tabBtn.classList.add("active");
				notificationState.tab = tabBtn.dataset.tab || "all";
				await loadNotificationPreview();
			});
		});

		markAllBtn?.addEventListener("click", async () => {
			try {
				await markAllAsRead();
				await loadNotificationPreview();
			} catch (error) {
				console.warn("Không thể danh dau tất cả đã đọc", error);
			}
		});

		notificationList.addEventListener("click", async (event) => {
			const actionBtn = event.target.closest("button[data-action]");
			const item = event.target.closest(".notification-item[data-id]");
			if (!item) return;

			const maThongBao = Number(item.dataset.id || "0");
			if (!maThongBao) return;

			if (actionBtn) {
				event.preventDefault();
				event.stopPropagation();

				try {
					if (actionBtn.dataset.action === "mark") {
						await markOneAsRead(maThongBao);
					}
					if (actionBtn.dataset.action === "delete") {
						await deleteOne(maThongBao);
					}
					await loadNotificationPreview();
				} catch (error) {
					console.warn("Không thể xu ly thong bao", error);
				}
				return;
			}

			const url = item.dataset.url;
			try {
				if (item.classList.contains("unread")) {
					await markOneAsRead(maThongBao);
				}
			} catch (error) {
				console.warn("Không thể cập nhật đã đọc truoc khi dieu huong", error);
			}

			if (url) {
				window.location.href = url;
			}
		});

		notificationWrap.addEventListener("shown.bs.dropdown", () => {
			loadNotificationPreview();
		});

		loadNotificationSummary();
		setInterval(() => {
			loadNotificationSummary();
		}, notificationState.pollingMs);
	}

	setupTopbarSearch();
})();


