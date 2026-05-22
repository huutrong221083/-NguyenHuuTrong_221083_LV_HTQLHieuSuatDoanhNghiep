        const canManageTask = @Html.Raw(Json.Serialize(canManageTask));
        const canEditTask = @Html.Raw(Json.Serialize(canEditTask));
        const canDeleteTask = canManageTask;
        const canDragTask = canManageTask;

        const state = {
            tasks: [],
            filteredTasks: [],
            progressUpdates: [],
            filteredProgressUpdates: [],
            projects: [],
            employees: [],
            teams: [],
            departments: [],
            selectedTaskId: null,
            editingTaskId: null,
            viewMode: 'list',
            comments: new Map(),
            docs: new Map(),
            replyToCommentId: null,
            currentDetail: null,
            refreshTimer: null
        };

        const el = {
            filterKeyword: document.getElementById('filterKeyword'),
            filterAssignee: document.getElementById('filterAssignee'),
            filterProject: document.getElementById('filterProject'),
            filterStatus: document.getElementById('filterStatus'),
            filterPriority: document.getElementById('filterPriority'),
            filterDifficulty: document.getElementById('filterDifficulty'),
            filterDeadline: document.getElementById('filterDeadline'),
            filterStartFrom: document.getElementById('filterStartFrom'),
            filterStartTo: document.getElementById('filterStartTo'),
            btnApplyFilters: document.getElementById('btnApplyFilters'),
            btnResetFilters: document.getElementById('btnResetFilters'),
            btnExportTasks: document.getElementById('btnExportTasks'),
            btnOpenTaskForm: document.getElementById('btnOpenTaskForm'),
            btnListView: document.getElementById('btnListView'),
            btnBoardView: document.getElementById('btnBoardView'),
            listViewWrap: document.getElementById('listViewWrap'),
            boardViewWrap: document.getElementById('boardViewWrap'),
            taskTableBody: document.getElementById('taskTableBody'),
            kanbanTodo: document.getElementById('kanbanTodo'),
            kanbanDoing: document.getElementById('kanbanDoing'),
            kanbanDone: document.getElementById('kanbanDone'),
            kanbanPending: document.getElementById('kanbanPending'),
            countTodo: document.getElementById('countTodo'),
            countDoing: document.getElementById('countDoing'),
            countDone: document.getElementById('countDone'),
            countPending: document.getElementById('countPending'),
            kpiPersonalScore: document.getElementById('kpiPersonalScore'),
            kpiGoalRate: document.getElementById('kpiGoalRate'),
            kpiOnTime: document.getElementById('kpiOnTime'),
            kpiWeeklyTrend: document.getElementById('kpiWeeklyTrend'),
            kpiPersonalScoreBar: document.getElementById('kpiPersonalScoreBar'),
            kpiGoalRateBar: document.getElementById('kpiGoalRateBar'),
            kpiOnTimeBar: document.getElementById('kpiOnTimeBar'),
            kpiWeeklyTrendBar: document.getElementById('kpiWeeklyTrendBar'),
            activityFeedGlobal: document.getElementById('activityFeedGlobal'),
            taskDetailDrawer: document.getElementById('taskDetailDrawer'),
            taskDetailBackdrop: document.getElementById('taskDetailBackdrop'),
            btnCloseDetail: document.getElementById('btnCloseDetail'),
            detailTitle: document.getElementById('detailTitle'),
            detailContent: document.getElementById('detailContent'),
            focusInsight: document.getElementById('focusInsight'),
            todayTaskCount: document.getElementById('todayTaskCount'),
            overdueCount: document.getElementById('overdueCount'),
            onTimePercent: document.getElementById('onTimePercent'),
            currentKpi: document.getElementById('currentKpi'),
            notificationCount: document.getElementById('notificationCount')
        };

        const escapeHtml = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');

        function formatDate(dateStr) {
            if (!dateStr) return '-';
            const date = new Date(dateStr);
            return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
        }

        function formatDateTime(dateStr) {
            if (!dateStr) return '-';
            const date = new Date(dateStr);
            return date.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
        }

        function toInputDate(dateStr) {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            return date.toISOString().split('T')[0];
        }

        function notify(message, type = 'info') {
            if (window.UiToast && typeof window.UiToast.show === 'function') {
                window.UiToast.show({ message, type });
                return;
            }

            window.alert(type === 'error' ? `Lỗi: ${message}` : message);
        }

        function mapApprovalBadge(status) {
            if (status === 'Đã duyệt') {
                return '<span class="status-badge status-done">Đã duyệt</span>';
            }

            if (status === 'Từ chối') {
                return '<span class="status-badge status-overdue">Từ chối</span>';
            }

            return '<span class="status-badge status-doing">Chờ duyệt</span>';
        }

        function getLatestProgressApproval(task) {
            const history = (task?.lichSuTienDo || []).slice();
            if (!history.length) return null;

            history.sort((a, b) => {
                const left = new Date(a.ngayPheDuyet || a.ngayCapNhat || 0).getTime();
                const right = new Date(b.ngayPheDuyet || b.ngayCapNhat || 0).getTime();
                return right - left;
            });

            const latest = history[0] || null;
            if (!latest) return null;

            return {
                status: latest.trangThaiPheDuyet || 'Chờ duyệt',
                ngayPheDuyet: latest.ngayPheDuyet || null,
                lyDoTuChoi: latest.lyDoTuChoi || '',
                nguoiPheDuyet: latest.hoTenNguoiPheDuyet || latest.nguoiPheDuyet || ''
            };
        }

        function resolveApproverName(item) {
            const directName = item?.hoTenNguoiPheDuyet || item?.nguoiPheDuyetTen || item?.tenNguoiPheDuyet;
            if (directName && String(directName).trim()) {
                return String(directName).trim();
            }

            const approverIdRaw = item?.nguoiPheDuyet ?? item?.NguoiPheDuyet;
            const approverId = Number(approverIdRaw || 0);
            if (approverId && Array.isArray(state.employees)) {
                const employee = state.employees.find(emp => Number(emp.maNhanVien || emp.MaNhanVien || 0) === approverId);
                const employeeName = employee?.hoTen || employee?.HoTen;
                if (employeeName && String(employeeName).trim()) {
                    return String(employeeName).trim();
                }
            }

            if (approverIdRaw !== undefined && approverIdRaw !== null && String(approverIdRaw).trim()) {
                return String(approverIdRaw).trim();
            }

            return 'Chưa duyệt';
        }

        function buildProgressHistoryHtml(task) {
            const history = (task?.lichSuTienDo || []).slice();
            if (!history.length) {
                return '<div style="font-size:12px; color: var(--text-muted);">Chưa có lần cập nhật tiến độ nào trước đó.</div>';
            }

            history.sort((a, b) => {
                const left = new Date(a.ngayCapNhat || a.ngayPheDuyet || 0).getTime();
                const right = new Date(b.ngayCapNhat || b.ngayPheDuyet || 0).getTime();
                return right - left;
            });

            return `
                <div class="table-wrapper" style="margin-top:0;">
                    <table class="task-table" style="width:100%;">
                        <thead>
                            <tr>
                                <th style="white-space:nowrap;">Ngày cập nhật</th>
                                <th style="white-space:nowrap;">Tiến độ</th>
                                <th style="white-space:nowrap;">Trạng thái duyệt</th>
                                <th style="white-space:nowrap;">Người duyệt</th>
                                <th style="white-space:nowrap;">Ngày duyệt</th>
                                <th style="white-space:nowrap;">Lý do</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${history.map(item => {
                                const progress = Number(item.phanTramHoanThanh || 0);
                                const status = item.trangThaiPheDuyet || 'Chờ duyệt';
                                const approverName = resolveApproverName(item);

                                return `
                                    <tr>
                                        <td>${formatDateTime(item.ngayCapNhat)}</td>
                                        <td>
                                            <div class="progress progress-compact mb-1"><div class="progress-bar" style="width:${progress}%"></div></div>
                                            <small>${progress.toFixed(0)}%</small>
                                        </td>
                                        <td>${mapApprovalBadge(status)}</td>
                                        <td>
                                            <div class="fw-bold">${escapeHtml(approverName)}</div>
                                        </td>
                                        <td>${item.ngayPheDuyet ? formatDateTime(item.ngayPheDuyet) : '-'}</td>
                                        <td>
                                            ${status === 'Từ chối'
                                                ? `<span style="color:#991b1b;">${escapeHtml(item.lyDoTuChoi || 'Chưa có lý do')}</span>`
                                                : '<span style="color:var(--text-muted);">-</span>'}
                                        </td>
                                    </tr>
                                `;
                            }).join('')}
                        </tbody>
                    </table>
                </div>
            `;
        }

        function toggleProgressHistory() {
            const panel = document.getElementById('detailProgressHistoryPanel');
            const button = document.getElementById('btnToggleProgressHistory');
            if (!panel || !button) return;

            const shouldOpen = panel.style.display === 'none';
            panel.style.display = shouldOpen ? 'block' : 'none';
            button.textContent = shouldOpen ? 'Ẩn lịch sử cập nhật' : 'Xem lịch sử cập nhật';
        }

        function syncProgressUpdateTaskOptions() {
            // Function removed - section 2 deleted
        }

        function applyProgressFilters() {
            // Function removed - section 2 deleted
        }

        function renderProgressUpdates() {
            // Function removed - section 2 deleted
        }

        async function loadProgressUpdates() {
            // Function removed - section 2 deleted
        }

        async function requestJson(url, options = {}) {
            try {
                const res = await fetch(url, { ...options });

                if (!res.ok) {
                    // try to read text body for better diagnostics
                    let text = null;
                    try { text = await res.text(); } catch (e) { /* ignore */ }
                    console.error('Fetch error', url, res.status, res.statusText, text);
                    return { success: false, status: res.status, message: text || res.statusText || 'HTTP error' };
                }

                const contentType = res.headers.get('content-type') || '';
                if (contentType.includes('application/json')) {
                    const txt = await res.text();
                    if (!txt) {
                        return { success: false, message: 'Empty JSON response', status: res.status };
                    }
                    return JSON.parse(txt);
                }

                // Fallback: return raw text
                const txt = await res.text();
                return { success: true, data: txt };
            } catch (error) {
                console.error('Request error:', error);
                return { success: false, message: error?.message || 'Lỗi kết nối' };
            }
        }

        function getPayload(resp) {
            return resp?.data ?? resp?.Data ?? null;
        }

        function extractArray(resp) {
            if (!resp || resp.success === false) return [];
            const data = getPayload(resp);
            if (!data) return [];
            if (Array.isArray(data)) return data;
            if (Array.isArray(data.items)) return data.items;
            if (Array.isArray(data.Items)) return data.Items;
            return [];
        }

        async function loadReferenceData() {
            const [projectRes, employeeRes] = await Promise.all([
                requestJson('/duan'),
                requestJson('/nhanvien')
            ]);

            state.projects = extractArray(projectRes);
            const projectOptions = state.projects
                .map(p => {
                    const projectId = p.maDuAn ?? p.MaDuAn ?? '';
                    const projectName = p.tenDuAn ?? p.TenDuAn ?? '';
                    return `<option value="${projectId}">${escapeHtml(projectName)}</option>`;
                })
                .join('');
            el.filterProject.innerHTML = '<option value="">Tất cả</option>' + projectOptions;
            // Mirror options into compact toolbar if present
            const compactProj = document.getElementById('filterProjectCompact');
            if (compactProj) compactProj.innerHTML = el.filterProject.innerHTML;

            state.employees = extractArray(employeeRes);
            const employeeOptions = state.employees
                .map(e => {
                    const employeeId = e.maNhanVien ?? e.MaNhanVien ?? '';
                    const employeeName = e.hoTen ?? e.HoTen ?? '';
                    return `<option value="${employeeId}">${escapeHtml(employeeName)}</option>`;
                })
                .join('');
            el.filterAssignee.innerHTML = '<option value="">Tất cả</option>' + employeeOptions;

            // Mirror status/priority options if compact selects exist
            const compactStatus = document.getElementById('filterStatusCompact');
            if (compactStatus && el.filterStatus) compactStatus.innerHTML = el.filterStatus.innerHTML;
            const compactPriority = document.getElementById('filterPriorityCompact');
            if (compactPriority && el.filterPriority) compactPriority.innerHTML = el.filterPriority.innerHTML;
        }

        async function loadTasks() {
            if (el.taskTableBody) {
                el.taskTableBody.innerHTML = `<tr><td colspan="10" style="text-align:center; padding:20px; color: var(--text-muted);">Đang tải công việc...</td></tr>`;
            }

            const res = await requestJson('/congviec');
            if (!res || res.success === false) {
                if (el.taskTableBody) {
                    el.taskTableBody.innerHTML = `<tr><td colspan="10" style="text-align:center; padding:20px; color: var(--danger);">${escapeHtml(res?.message || 'Không thể tải dữ liệu công việc')}</td></tr>`;
                }
                notify(`Không thể tải danh sách công việc: ${res?.message || 'Lỗi không xác định'}`, 'error');
                state.tasks = [];
                state.filteredTasks = [];
                renderTasks();
                return;
            }

            // Extract list from PagedResult or plain array
            const data = getPayload(res);
            if (Array.isArray(data)) {
                state.tasks = data;
            } else if (data && Array.isArray(data.items)) {
                state.tasks = data.items;
            } else if (data && Array.isArray(data.Items)) {
                state.tasks = data.Items;
            } else {
                state.tasks = [];
            }
            syncProgressUpdateTaskOptions();
            applyFilters();
        }

        function applyFilters() {
            const keyword = el.filterKeyword.value.toLowerCase();
            const projectId = el.filterProject.value;
            const statusId = el.filterStatus.value;
            const priorityId = el.filterPriority.value;
            const difficultyId = el.filterDifficulty.value;
            const deadlineFilter = el.filterDeadline.value;
            const assigneeId = el.filterAssignee.value;
            const startFrom = el.filterStartFrom.value ? new Date(el.filterStartFrom.value) : null;
            const startTo = el.filterStartTo.value ? new Date(el.filterStartTo.value) : null;

            const now = new Date();
            const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

            state.filteredTasks = state.tasks.filter(task => {
                // Keyword search
                if (keyword && !(`${task.tenCongViec} ${task.moTa}`.toLowerCase().includes(keyword))) return false;

                // Project filter
                if (projectId && task.maDuAn !== Number(projectId)) return false;

                // Status filter
                if (statusId && task.maTrangThai !== Number(statusId)) return false;

                // Priority filter
                if (priorityId && task.maDoUuTien !== Number(priorityId)) return false;

                // Difficulty filter
                if (difficultyId && task.maDoKho !== Number(difficultyId)) return false;

                // Assignee filter
                if (assigneeId && (!task.nguoiDuocGiao || !task.nguoiDuocGiao.some(a => a.maNhanVien === Number(assigneeId)))) return false;

                // Deadline filter
                if (deadlineFilter && task.hanHoanThanh) {
                    const deadlineDate = new Date(task.hanHoanThanh);
                    if (deadlineFilter === 'today' && deadlineDate.getTime() !== today.getTime()) return false;
                    if (deadlineFilter === 'week' && deadlineDate > new Date(today.getTime() + 3 * 24 * 60 * 60 * 1000)) return false;
                    if (deadlineFilter === 'month' && deadlineDate > new Date(today.getTime() + 30 * 24 * 60 * 60 * 1000)) return false;
                    if (deadlineFilter === 'overdue' && deadlineDate >= today) return false;
                }

                // Date range filter
                if (startFrom && task.ngayBatDau) {
                    const taskDate = new Date(task.ngayBatDau);
                    if (taskDate < startFrom) return false;
                }
                if (startTo && task.ngayBatDau) {
                    const taskDate = new Date(task.ngayBatDau);
                    if (taskDate > startTo) return false;
                }

                return true;
            });

            renderTasks();
        }

        function renderTasks() {
            if (state.viewMode === 'list') {
                renderTableView();
            } else {
                renderKanbanView();
            }

            // Update overview KPI numbers (for Admin/Manager view)
            try {
                const useArr = (state.filteredTasks && state.filteredTasks.length) ? state.filteredTasks : state.tasks;
                const totalCount = useArr.length;
                const doing = useArr.filter(t => t.maTrangThai === 2).length;
                const done = useArr.filter(t => t.maTrangThai === 3).length;
                const overdue = useArr.filter(t => t.hanHoanThanh && new Date(t.hanHoanThanh) < new Date()).length;
                const onTimeRate = totalCount ? Math.round((done / totalCount) * 100) : 0;

                const elTotal = document.getElementById('totalTasks');
                if (elTotal) elTotal.textContent = totalCount;
                const eDoing = document.getElementById('countDoingOverview'); if (eDoing) eDoing.textContent = doing;
                const eDone = document.getElementById('countDoneOverview'); if (eDone) eDone.textContent = done;
                const eOverdue = document.getElementById('countOverdueOverview'); if (eOverdue) eOverdue.textContent = overdue;
                const eOnTime = document.getElementById('onTimeRateOverview'); if (eOnTime) eOnTime.textContent = onTimeRate + '%';
            } catch (e) { /* ignore */ }
        }

        function renderTableView() {
            const rows = state.filteredTasks.map(task => {
                const deadline = formatDate(task.hanHoanThanh);
                const priorityClass = task.maDoUuTien === 1 ? 'priority-low' : task.maDoUuTien === 2 ? 'priority-medium' : 'priority-high';
                const statusClass = task.maTrangThai === 1 ? 'status-todo' : task.maTrangThai === 2 ? 'status-doing' : task.maTrangThai === 3 ? 'status-done' : 'status-overdue';
                const statusText = task.maTrangThai === 1 ? 'Chưa bắt đầu' : task.maTrangThai === 2 ? 'Đang làm' : task.maTrangThai === 3 ? 'Hoàn thành' : 'Tạm hoãn';
                const progress = task.tienDoPhanTram || 0;

                return `
                    <tr onclick="openTask(${task.maCongViec})">
                        ${canManageTask ? `<td class="task-code">#${task.maCongViec}</td>` : ''}
                        <td class="task-name">${escapeHtml(task.tenCongViec || '')}</td>
                        <td>${escapeHtml(task.tenDuAn || '-')}</td>
                        <td>${deadline}</td>
                        <td><span class="priority-badge ${priorityClass}">${task.maDoUuTien === 1 ? 'Thấp' : task.maDoUuTien === 2 ? 'Trung bình' : 'Cao'}</span></td>
                        <td>
                            <div class="progress-bar">
                                <div class="progress-fill" style="width: ${progress}%"></div>
                            </div>
                            <span style="font-size: 11px; color: var(--text-muted);">${progress}%</span>
                        </td>
                        <td><span class="status-badge ${statusClass}">${statusText}</span></td>
                        ${canManageTask ? `<td>${task.maDoKho === 1 ? 'Dễ' : task.maDoKho === 2 ? 'Trung bình' : 'Khó'}</td>` : ''}
                        <td><button onclick="openTask(${task.maCongViec}); event.stopPropagation();" class="btn" title="Xem" style="padding:6px; font-size:12px;"><i class="bi bi-eye"></i></button></td>
                    </tr>
                `;
            }).join('');

            el.taskTableBody.innerHTML = rows || `<tr><td colspan="10" style="text-align: center; padding: 20px; color: var(--text-muted);">Không có công việc</td></tr>`;
        }

        function renderKanbanView() {
            const statuses = [
                { id: 1, el: el.kanbanTodo, count: el.countTodo },
                { id: 2, el: el.kanbanDoing, count: el.countDoing },
                { id: 4, el: el.kanbanPending, count: el.countPending },
                { id: 3, el: el.kanbanDone, count: el.countDone }
            ];

            statuses.forEach(status => {
                const tasks = state.filteredTasks.filter(t => t.maTrangThai === status.id);
                status.count.textContent = tasks.length;

                status.el.innerHTML = tasks.map(task => `
                    <div class="kanban-card" onclick="openTask(${task.maCongViec})">
                        <div class="kanban-card-title">${escapeHtml(task.tenCongViec || '')}</div>
                        <div class="kanban-card-meta">
                            <span>${formatDate(task.hanHoanThanh)}</span>
                            <span style="font-weight: 600; color: var(--primary);">${task.tienDoPhanTram || 0}%</span>
                        </div>
                        <div class="kanban-card-progress">
                            <div class="progress-fill" style="width: ${task.tienDoPhanTram || 0}%"></div>
                        </div>
                    </div>
                `).join('');
            });
        }

        async function openTask(taskId) {
            const task = state.tasks.find(t => t.maCongViec === taskId);

            let detailTask = task || null;
            const detailRes = await requestJson(`/congviec/${taskId}`);
            if (detailRes && detailRes.success) {
                const detailPayload = getPayload(detailRes);
                if (detailPayload && typeof detailPayload === 'object') {
                    detailTask = { ...(task || {}), ...detailPayload };
                }
            }

            if (!detailTask) {
                notify('Không thể tải chi tiết công việc này', 'error');
                return;
            }

            if (!task) {
                state.tasks.push(detailTask);
                applyFilters();
            }

            state.selectedTaskId = taskId;
            state.currentDetail = detailTask;

            el.detailTitle.textContent = detailTask.tenCongViec || 'Công việc';
            
            const statusText = detailTask.maTrangThai === 1 ? 'Chưa bắt đầu' : detailTask.maTrangThai === 2 ? 'Đang làm' : detailTask.maTrangThai === 3 ? 'Hoàn thành' : 'Tạm hoãn';
            const priorityText = detailTask.maDoUuTien === 1 ? 'Thấp' : detailTask.maDoUuTien === 2 ? 'Trung bình' : 'Cao';
            const latestApproval = getLatestProgressApproval(detailTask);
            const progressHistoryHtml = buildProgressHistoryHtml(detailTask);
            const approvalStatusBlock = latestApproval
                ? `
                    <div class="detail-section">
                        <div style="display:flex; justify-content:space-between; align-items:center; gap:8px; margin-bottom:8px;">
                            <h3 class="detail-section-title" style="margin:0;">Trạng thái duyệt tiến độ</h3>
                            <button id="btnToggleProgressHistory" class="btn btn-secondary" type="button" style="padding:6px 10px; font-size:12px;" onclick="toggleProgressHistory()">Xem lịch sử cập nhật</button>
                        </div>
                        <div style="padding: 12px; border: 1px solid var(--border); border-radius: 8px; background: var(--bg-light);">
                            <div style="display:flex; justify-content:space-between; align-items:center; gap:8px; margin-bottom:8px;">
                                <strong style="font-size:13px; color: var(--text-dark);">Kết quả duyệt</strong>
                                ${mapApprovalBadge(latestApproval.status)}
                            </div>
                            <div style="font-size:12px; color:var(--text-muted); margin-bottom:6px;">
                                ${escapeHtml(resolveApproverName(latestApproval) || '-')}
                            </div>
                            <div style="font-size:12px; color:var(--text-muted); margin-bottom:6px;">
                                Ngày duyệt: <strong style="color: var(--text-dark);">${latestApproval.ngayPheDuyet ? formatDate(latestApproval.ngayPheDuyet) : '-'}</strong>
                            </div>
                            ${latestApproval.status === 'Từ chối'
                                ? `<div style="font-size:12px; color:#991b1b; background:#fee2e2; border:1px solid #fecaca; border-radius:6px; padding:8px;">Lý do từ chối: ${escapeHtml(latestApproval.lyDoTuChoi || 'Chưa có lý do')}</div>`
                                : ''}
                            <div id="detailProgressHistoryPanel" style="display:none; margin-top:10px;">
                                ${progressHistoryHtml}
                            </div>
                        </div>
                    </div>
                `
                : '';

            el.detailContent.innerHTML = `
                <div class="detail-section">
                    <div class="detail-grid">
                        <div class="detail-field">
                            <div class="detail-field-label">Dự án</div>
                            <div class="detail-field-value">${escapeHtml(detailTask.tenDuAn || '-')}</div>
                        </div>
                        <div class="detail-field">
                            <div class="detail-field-label">Deadline</div>
                            <div class="detail-field-value">${formatDate(detailTask.hanHoanThanh)}</div>
                        </div>
                        <div class="detail-field">
                            <div class="detail-field-label">Tiến độ</div>
                            <div class="detail-field-value">${detailTask.tienDoPhanTram || 0}%</div>
                        </div>
                        <div class="detail-field">
                            <div class="detail-field-label">Ưu tiên</div>
                            <div class="detail-field-value">${priorityText}</div>
                        </div>
                    </div>
                </div>
                <div class="detail-section">
                    <h3 class="detail-section-title">Mô tả chi tiết</h3>
                    <p style="color: var(--text-dark); font-size: 13px; line-height: 1.6;">${escapeHtml(detailTask.moTa || 'Chưa có mô tả')}</p>
                </div>
                <div class="detail-section">
                    <h3 class="detail-section-title">Cập nhật tiến độ</h3>
                    <div style="padding: 12px; border: 1px solid var(--border); border-radius: 8px; background: var(--bg-light);">
                        <input id="detailProgressPercent" type="number" min="0" max="100" value="${detailTask.tienDoPhanTram || 0}" class="form-control" placeholder="Tiến độ %" style="margin-bottom: 8px;">
                        <input id="detailProgressNote" type="text" class="form-control" placeholder="Ghi chú cập nhật..." style="margin-bottom: 8px;">
                        <div class="detail-update-actions"><button class="btn btn-primary" style="width: auto;" onclick="saveProgress(${taskId})"><i class="bi bi-download"></i> Lưu tiến độ</button></div>
                    </div>
                </div>
                ${approvalStatusBlock}
                <div class="detail-section">
                    <h3 class="detail-section-title">Thao tác</h3>
                    <div style="display: flex; gap: 8px; flex-wrap: wrap;">
                        ${currentRole !== 'employee' ? `<button class="btn btn-secondary" onclick="editTask(${taskId})"><i class="bi bi-pencil"></i> Sửa</button>` : ''}
                        <button class="btn btn-success" onclick="completeTask(${taskId})"><i class="bi bi-check-circle"></i> Hoàn thành</button>
                        ${currentRole !== 'employee' ? `<button class="btn btn-danger" onclick="deleteTask(${taskId})"><i class="bi bi-trash"></i> Xóa</button>` : ''}
                    </div>
                </div>
            `;

            document.body.classList.add('detail-drawer-open');
            el.taskDetailDrawer.scrollTop = 0;
            el.detailContent.scrollTop = 0;
            el.taskDetailDrawer.classList.add('open');
            el.taskDetailBackdrop.classList.add('open');
        }

        function closeDetailDrawer() {
            el.taskDetailDrawer.classList.remove('open');
            el.taskDetailBackdrop.classList.remove('open');
            document.body.classList.remove('detail-drawer-open');
        }

        async function saveProgress(taskId) {
            const progressPercent = document.getElementById('detailProgressPercent')?.value;
            if (!progressPercent || progressPercent < 0 || progressPercent > 100) {
                notify('Vui lòng nhập tiến độ từ 0 đến 100', 'warning');
                return;
            }

            // Server exposes a dedicated progress endpoint at POST /tiendo
            const body = {
                MaCongViec: taskId,
                PhanTramHoanThanh: Number(progressPercent),
                GhiChu: document.getElementById('detailProgressNote')?.value || ''
            };

            const triggerButton = document.activeElement && document.activeElement.matches('button')
                ? document.activeElement
                : null;

            const res = await (window.UiButtonLoading?.run
                ? window.UiButtonLoading.run(triggerButton, () => requestJson('/tiendo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                }), { profile: 'save' })
                : requestJson('/tiendo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                }));

            if (res.success) {
                notify('Đã cập nhật tiến độ', 'success');
                await loadTasks();
                await openTask(taskId);
            } else {
                notify('Lỗi khi cập nhật tiến độ: ' + (res.message || res.status || 'Không xác định'), 'error');
            }
        }

        async function editTask(taskId) {
            notify('Chức năng sửa sẽ được phát triển', 'info');
        }

        async function completeTask(taskId) {
            // Use the status update endpoint
            const triggerButton = document.activeElement && document.activeElement.matches('button')
                ? document.activeElement
                : null;

            const res = await (window.UiButtonLoading?.run
                ? window.UiButtonLoading.run(triggerButton, () => requestJson(`/congviec/${taskId}/status`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ MaTrangThai: 3 })
                }), { profile: 'save' })
                : requestJson(`/congviec/${taskId}/status`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ MaTrangThai: 3 })
                }));

            if (res.success) {
                notify('Đã cập nhật trạng thái', 'success');
                await loadTasks();
                closeDetailDrawer();
            } else {
                notify('Lỗi khi cập nhật trạng thái: ' + (res.message || res.status || 'Không xác định'), 'error');
            }
        }

        async function deleteTask(taskId) {
            if (!confirm('Bạn có chắc muốn xóa công việc này?')) return;

            const triggerButton = document.activeElement && document.activeElement.matches('button')
                ? document.activeElement
                : null;

            const res = await (window.UiButtonLoading?.run
                ? window.UiButtonLoading.run(triggerButton, () => requestJson(`/congviec/${taskId}`, {
                    method: 'DELETE'
                }), { profile: 'delete' })
                : requestJson(`/congviec/${taskId}`, {
                    method: 'DELETE'
                }));

            if (res.success) {
                notify('Đã xóa công việc', 'success');
                await loadTasks();
                closeDetailDrawer();
            }
        }

        function setViewMode(mode) {
            state.viewMode = mode;
            if (mode === 'list') {
                el.listViewWrap.style.display = '';
                el.boardViewWrap.style.display = 'none';
                el.btnListView.classList.add('active');
                el.btnBoardView.classList.remove('active');
            } else {
                el.listViewWrap.style.display = 'none';
                el.boardViewWrap.style.display = '';
                el.btnListView.classList.remove('active');
                el.btnBoardView.classList.add('active');
            }
            renderTasks();
        }

        function resetTaskFiltersToDefault() {
            if (el.filterKeyword) el.filterKeyword.value = '';
            if (el.filterProject) el.filterProject.value = '';
            if (el.filterStatus) el.filterStatus.value = '';
            if (el.filterPriority) el.filterPriority.value = '';
            if (el.filterDifficulty) el.filterDifficulty.value = '';
            if (el.filterDeadline) el.filterDeadline.value = '';
            if (el.filterAssignee) el.filterAssignee.value = '';
            if (el.filterStartFrom) el.filterStartFrom.value = '';
            if (el.filterStartTo) el.filterStartTo.value = '';

            document.querySelectorAll('[data-quick-filter]').forEach(btn => {
                btn.classList.toggle('active', btn.getAttribute('data-quick-filter') === 'all');
            });
        }

        // Event listeners
        el.filterKeyword.addEventListener('input', applyFilters);
        el.filterProject.addEventListener('change', applyFilters);
        el.filterStatus.addEventListener('change', applyFilters);
        el.filterPriority.addEventListener('change', applyFilters);
        el.filterDifficulty.addEventListener('change', applyFilters);
        el.filterDeadline.addEventListener('change', applyFilters);
        el.filterAssignee.addEventListener('change', applyFilters);
        el.filterStartFrom.addEventListener('change', applyFilters);
        el.filterStartTo.addEventListener('change', applyFilters);
        if (el.btnApplyFilters) el.btnApplyFilters.addEventListener('click', applyFilters);
        el.btnResetFilters.addEventListener('click', () => {
            el.filterKeyword.value = '';
            el.filterProject.value = '';
            el.filterStatus.value = '';
            el.filterPriority.value = '';
            el.filterDifficulty.value = '';
            el.filterDeadline.value = '';
            el.filterAssignee.value = '';
            el.filterStartFrom.value = '';
            el.filterStartTo.value = '';
            applyFilters();
        });

        function exportTasksExcel() {
            const rows = (state.filteredTasks && state.filteredTasks.length ? state.filteredTasks : state.tasks) || [];
            const headers = ['Mã công việc', 'Tên công việc', 'Dự án', 'Hạn hoàn thành', 'Ưu tiên', 'Tiến độ', 'Trạng thái', 'Độ khó'];
            const escapeHtmlCell = (v) => String(v ?? '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
            const toPriority = (v) => v === 1 ? 'Thấp' : v === 2 ? 'Trung bình' : v === 3 ? 'Cao' : '';
            const toStatus = (v) => v === 1 ? 'Chưa bắt đầu' : v === 2 ? 'Đang làm' : v === 3 ? 'Hoàn thành' : v === 4 ? 'Tạm hoãn' : '';
            const toDifficulty = (v) => v === 1 ? 'Dễ' : v === 2 ? 'Trung bình' : v === 3 ? 'Khó' : '';

            const bodyRows = rows.map(t => [
                t.maCongViec,
                t.tenCongViec,
                t.tenDuAn,
                formatDate(t.hanHoanThanh),
                toPriority(t.maDoUuTien),
                `${t.tienDoPhanTram || 0}%`,
                toStatus(t.maTrangThai),
                toDifficulty(t.maDoKho)
            ].map(value => `<td>${escapeHtmlCell(value)}</td>`).join('')).map(cols => `<tr>${cols}</tr>`).join('');

            const headRow = headers.map(h => `<th>${escapeHtmlCell(h)}</th>`).join('');
            const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    table { border-collapse: collapse; font-family: Arial, sans-serif; font-size: 12pt; }
    th, td { border: 1px solid #cbd5e1; padding: 8px 10px; vertical-align: middle; }
    th { background: #2563eb; color: #ffffff; font-weight: 700; text-align: center; }
    tr:nth-child(even) td { background: #f8fafc; }
  </style>
</head>
<body>
  <table>
    <thead><tr>${headRow}</tr></thead>
    <tbody>${bodyRows}</tbody>
  </table>
</body>
</html>`;

            const blob = new Blob([`\uFEFF${html}`], { type: 'application/vnd.ms-excel;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            const stamp = new Date().toISOString().slice(0, 10);
            a.href = url;
            a.download = `cong-viec-${stamp}.xls`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
        }

        if (el.btnExportTasks) {
            el.btnExportTasks.addEventListener('click', exportTasksExcel);
        }

        el.btnListView.addEventListener('click', () => setViewMode('list'));
        el.btnBoardView.addEventListener('click', () => setViewMode('board'));
        el.btnCloseDetail.addEventListener('click', closeDetailDrawer);
        el.taskDetailBackdrop.addEventListener('click', closeDetailDrawer);
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closeDetailDrawer();
        });

        // Quick filter chips
        document.querySelectorAll('[data-quick-filter]').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('[data-quick-filter]').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');

                const mode = btn.getAttribute('data-quick-filter');
                el.filterProject.value = '';
                el.filterStatus.value = '';
                el.filterPriority.value = '';
                el.filterDeadline.value = '';
                if (mode === 'doing') el.filterStatus.value = '2';
                else if (mode === 'overdue') el.filterDeadline.value = 'overdue';
                else if (mode === 'high') el.filterPriority.value = '3';

                applyFilters();
            });
        });

        // Toggle advanced filters
        const advancedToggle = document.querySelector('.advanced-filter-toggle');
        if (advancedToggle) {
            advancedToggle.addEventListener('click', function() {
                const card = this.closest('.filter-card');
                const content = card ? card.querySelector('.advanced-filter-content') : null;
                if (!content) return;
                content.style.display = content.style.display === 'none' ? '' : 'none';
            });
        }

        // Initialize
        async function init() {
            await loadReferenceData();
            resetTaskFiltersToDefault();
            await loadTasks();

            // Bind compact toolbar selects to main filters
            const cp = document.getElementById('filterProjectCompact');
            const cs = document.getElementById('filterStatusCompact');
            const cpr = document.getElementById('filterPriorityCompact');
            if (cp) cp.addEventListener('change', () => { el.filterProject.value = cp.value; applyFilters(); });
            if (cs) cs.addEventListener('change', () => { el.filterStatus.value = cs.value; applyFilters(); });
            if (cpr) cpr.addEventListener('change', () => { el.filterPriority.value = cpr.value; applyFilters(); });
            const btnResetCompact = document.getElementById('btnResetFiltersCompact');
            if (btnResetCompact) btnResetCompact.addEventListener('click', () => {
                resetTaskFiltersToDefault();
                applyFilters();
            });

            // Nếu URL chứa openTaskId thì tự động mở chi tiết công việc tương ứng
            try {
                const params = new URLSearchParams(window.location.search);
                const openTaskId = params.get('openTaskId') || params.get('open');
                if (openTaskId) {
                    const idNum = Number(openTaskId);
                    if (idNum && typeof openTask === 'function') {
                        // delay một chút để UI và state đã sẵn sàng
                        setTimeout(() => openTask(idNum), 200);
                    }
                }
            } catch (e) {
                /* ignore */
            }

            setViewMode('list');
        }

        init();
