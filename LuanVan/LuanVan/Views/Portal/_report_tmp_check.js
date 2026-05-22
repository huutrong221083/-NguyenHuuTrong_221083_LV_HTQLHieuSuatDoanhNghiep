
    const roleKey = '@roleKey';
    const modals = {
        viewReportModal: document.getElementById('viewReportModal'),
        emailModal: document.getElementById('emailModal'),
        deleteModal: document.getElementById('deleteModal'),
        reviewModal: document.getElementById('reviewModal'),
        requestModal: document.getElementById('requestModal')
    };
    const modalInstances = {};

    let allReports = [];
    let allRequests = [];
    let teamEmployees = [];
    let scopeStats = null;
    let kpiReportStats = null;
    let currentStatusFilter = '';
    let currentReportIdForDelete = null;
    let currentReportIdForReview = null;
    let currentReportIdForView = null;
    const mineOnlyView = new URLSearchParams(window.location.search).get('mine') === '1';

    function getDateRangeFromPeriod(periodKey) {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const toIso = (d) => d.toISOString().slice(0, 10);
        let from = null;
        let to = null;
        switch (String(periodKey || '')) {
            case 'today':
                from = today;
                to = today;
                break;
            case 'this_week': {
                const day = (today.getDay() + 6) % 7; // Monday = 0
                from = new Date(today);
                from.setDate(today.getDate() - day);
                to = new Date(from);
                to.setDate(from.getDate() + 6);
                break;
            }
            case 'this_month':
                from = new Date(today.getFullYear(), today.getMonth(), 1);
                to = new Date(today.getFullYear(), today.getMonth() + 1, 0);
                break;
            case 'this_quarter': {
                const q = Math.floor(today.getMonth() / 3);
                from = new Date(today.getFullYear(), q * 3, 1);
                to = new Date(today.getFullYear(), q * 3 + 3, 0);
                break;
            }
            case 'this_year':
                from = new Date(today.getFullYear(), 0, 1);
                to = new Date(today.getFullYear(), 11, 31);
                break;
            default:
                break;
        }
        return { fromDate: from ? toIso(from) : null, toDate: to ? toIso(to) : null };
    }

    function getModalInstance(modalId) {
        const modalEl = modals[modalId];
        if (!modalEl || !window.bootstrap?.Modal || !modalEl.querySelector('.modal-dialog')) {
            return null;
        }

        if (!modalInstances[modalId]) {
            modalInstances[modalId] = new bootstrap.Modal(modalEl);
        }

        return modalInstances[modalId];
    }

    function openModal(modalId) {
        const modalEl = modals[modalId];
        if (!modalEl) return;
        const bsModal = getModalInstance(modalId);
        if (bsModal) {
            bsModal.show();
            return;
        }

        modalEl.classList.add('show');
        modalEl.style.display = 'block';
        modalEl.removeAttribute('aria-hidden');
        modalEl.setAttribute('aria-modal', 'true');
        document.body.classList.add('modal-open');
    }

    function closeModal(modalId) {
        const modalEl = modals[modalId];
        if (!modalEl) return;
        const bsModal = getModalInstance(modalId);
        if (bsModal) {
            bsModal.hide();
            return;
        }

        modalEl.classList.remove('show');
        modalEl.style.display = 'none';
        modalEl.setAttribute('aria-hidden', 'true');
        modalEl.removeAttribute('aria-modal');
        document.body.classList.remove('modal-open');
    }

    document.querySelectorAll('[data-modal]').forEach(btn =>
        btn.addEventListener('click', e => {
            const modalId = e.currentTarget?.dataset?.modal;
            if (modalId) closeModal(modalId);
        })
    );

    function normalizeReportStatus(status) {
        const raw = String(status || '').trim().toLowerCase();
        if (!raw) return '';
        if (raw.includes('approved') || raw.includes('đã duyệt') || raw.includes('da duyet') || raw === 'duyet') return 'approved';
        if (raw.includes('rejected') || raw.includes('từ chối') || raw.includes('tu choi') || raw === 'reject') return 'rejected';
        if (raw.includes('submitted') || raw.includes('đã gửi') || raw.includes('da gui') || raw === 'gui') return 'submitted';
        if (raw.includes('draft') || raw.includes('nháp') || raw.includes('nhap')) return 'draft';
        if (raw.includes('cancel')) return 'cancelled';
        if (raw.includes('overdue') || raw.includes('quá hạn') || raw.includes('qua han')) return 'overdue';
        return raw;
    }

    function getReportStatusLabel(status) {
        const normalized = normalizeReportStatus(status);
        const labels = {
            draft: 'Nháp',
            submitted: 'Đã gửi',
            approved: 'Đã duyệt',
            rejected: 'Từ chối',
            cancelled: 'Đã hủy',
            overdue: 'Quá hạn'
        };
        return labels[normalized] || (status || '-');
    }

    function getStatusBadgeClass(status) {
        const s = normalizeReportStatus(status);
        if (s === 'approved') return 'status-completed';
        if (s === 'rejected') return 'status-error';
        return 'status-processing';
    }

    function toVietnameseReportType(type) {
        const key = String(type || '').trim().toLowerCase();
        const map = {
            personal: 'Báo cáo cá nhân',
            project: 'Báo cáo dự án',
            department: 'Báo cáo phòng ban',
            ai: 'Báo cáo AI',
            admin: 'Báo cáo quản trị',
            daily: 'Báo cáo hằng ngày',
            weekly: 'Báo cáo hằng tuần',
            monthly: 'Báo cáo hằng tháng',
            quarterly: 'Báo cáo hằng quý',
            yearly: 'Báo cáo hằng năm'
        };
        return map[key] || (type || '-');
    }

    async function fetchReports(filters = {}) {
        const params = new URLSearchParams();
        if (filters.fromDate) params.append('NgayBatDau', filters.fromDate);
        if (filters.toDate) params.append('NgayKetThuc', filters.toDate);
        if (filters.reportType) params.append('LoaiBaoCao', filters.reportType);
        if (filters.maPhongBan) params.append('MaPhongBan', filters.maPhongBan);
        if (filters.keywordNhanVien) params.append('TuKhoaNhanVien', filters.keywordNhanVien);
        if (mineOnlyView) params.append('MineOnly', 'true');
        params.append('PageNumber', 1); params.append('PageSize', 100);
        const response = await fetch(`/api/baocao/list?${params.toString()}`);
        const data = response.ok ? await response.json() : { items: [] };
        allReports = (data.items || []).map(b => ({
            id: b.maBaoCao, name: b.tenBaoCao, typeLabel: toVietnameseReportType(b.loaiBaoCaoLabel || b.loaiBaoCao),
            creator: b.nguoiTao || '-', format: b.definDang || 'PDF',
            recipient: b.nguoiNhanBaoCao || '-',
            createdDate: b.ngayTao ? new Date(b.ngayTao).toLocaleString('vi-VN') : '-',
            period: (b.ngayBatDau && b.ngayKetThuc) ? `${new Date(b.ngayBatDau).toLocaleDateString('vi-VN')} - ${new Date(b.ngayKetThuc).toLocaleDateString('vi-VN')}` : '-',
            status: normalizeReportStatus(b.trangThai || b.trangThaiLabel),
            statusLabel: getReportStatusLabel(b.trangThaiLabel || b.trangThai)
        }));
        teamEmployees = data.teamEmployees || [];
        scopeStats = data.scopeStats || null;
        renderScopeStats();
        await fetchKpiReportSummary(filters);
    }

    async function fetchKpiReportSummary(filters = {}) {
        try {
            const params = new URLSearchParams();
            if (filters.fromDate) params.append('NgayBatDau', filters.fromDate);
            if (filters.toDate) params.append('NgayKetThuc', filters.toDate);
            if (filters.maPhongBan) params.append('MaPhongBan', filters.maPhongBan);

            const response = await fetch(`/api/report/kpi-summary?${params.toString()}`);
            const payload = response.ok ? await response.json() : null;
            kpiReportStats = payload?.data || null;
        } catch {
            kpiReportStats = null;
        }
        renderKpiReportStats();
    }

    function renderScopeStats() {
        const el = document.getElementById('scopeStats');
        if (!el) return;
        if (!scopeStats) {
            el.textContent = 'Scope: -';
            return;
        }
        if (mineOnlyView) {
            el.textContent = `Scope: Báo cáo của tôi • Tổng ${scopeStats.total || 0} • Đã duyệt ${scopeStats.approved || 0} • Từ chối ${scopeStats.rejected || 0}`;
            return;
        }
        const teamSize = teamEmployees?.length || 0;
        el.textContent = `Scope: Tổng ${scopeStats.total || 0} • Đã duyệt ${scopeStats.approved || 0} • Từ chối ${scopeStats.rejected || 0} • NV phạm vi ${teamSize}`;
    }

    function renderKpiReportStats() {
        const el = document.getElementById('kpiReportStats');
        if (!el) return;
        if (!kpiReportStats) {
            el.textContent = 'KPI Report: -';
            return;
        }
        el.textContent = `KPI Report: KPI TB ${Number(kpiReportStats.kpiTrungBinh || 0).toFixed(2)} • Đạt KPI ${Number(kpiReportStats.tyLeDatKpi || 0).toFixed(1)}% • Đúng hạn ${kpiReportStats.taskDungHan || 0} • Trễ hạn ${kpiReportStats.taskTreHan || 0}`;
    }

    function buildReportFiltersFromUi() {
        const periodRange = getDateRangeFromPeriod(document.getElementById('reportPeriodFilter').value);
        const departmentValue = document.getElementById('departmentFilter').value;
        const parsedDepartment = departmentValue ? Number(departmentValue) : null;
        return {
            fromDate: periodRange.fromDate,
            toDate: periodRange.toDate,
            reportType: document.getElementById('reportType').value || null,
            maPhongBan: Number.isNaN(parsedDepartment) ? null : parsedDepartment,
            keywordNhanVien: document.getElementById('employeeSearch').value?.trim() || null
        };
    }

    let reportFilterInputTimer = null;
    async function applyReportFiltersImmediate() {
        await fetchReports(buildReportFiltersFromUi());
        renderReportTable();
    }

    async function fetchRequests() {
        const response = await fetch('/api/report/request/list');
        const data = response.ok ? await response.json() : null;
        allRequests = data?.data || [];
    }

    async function loadDepartments() {
        const select = document.getElementById('departmentFilter');
        if (!select) return;

        try {
            const response = await fetch('/phongban');
            const payload = response.ok ? await response.json() : null;
            const items = Array.isArray(payload?.data)
                ? payload.data
                : Array.isArray(payload?.Data)
                    ? payload.Data
                    : Array.isArray(payload)
                        ? payload
                        : [];

            const currentValue = select.value;
            select.innerHTML = '<option value="">Tất cả phòng ban</option>' + items.map(item =>
                `<option value="${item.maPhongBan}">${escapeHtml(item.tenPhongBan || `Phòng ban #${item.maPhongBan}`)}</option>`
            ).join('');
            select.value = currentValue || '';
        } catch {
            // Keep the fallback empty option if the department API is unavailable.
        }
    }

    async function deleteBaoCaoApi(reportId) {
        const response = await fetch(`/api/baocao/${reportId}`, { method: 'DELETE' });
        return response.ok;
    }

    async function reviewReport(reportId, action, note) {
        const url = action === 'approve' ? '/api/report/review/approve' : '/api/report/review/reject';
        const response = await fetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ maBaoCao: reportId, note })
        });
        return response.ok;
    }

    async function createReportRequest(payload) {
        const response = await fetch('/api/report/request/create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const data = response.ok ? await response.json() : null;
        return { ok: response.ok, data };
    }

    function downloadBlob(blob, fileName) {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.URL.revokeObjectURL(url);
    }

    function sanitizeFileName(value) {
        return String(value || 'BaoCao')
            .replace(/[\\/:*?"<>|]+/g, '_')
            .replace(/\s+/g, ' ')
            .trim();
    }

    async function downloadReportPdf(reportId) {
        const report = allReports.find(x => x.id === reportId) || null;
        const detailRes = await fetch(`/api/report/detail/${reportId}`);
        if (!detailRes.ok) {
            alert('Không tải được dữ liệu báo cáo để xuất PDF.');
            return;
        }
        const detailPayload = await detailRes.json();
        const d = detailPayload?.data || detailPayload?.Data;
        if (!d) {
            alert('Báo cáo không có dữ liệu để xuất.');
            return;
        }

        const exportPayload = {
            tenBaoCao: d.tenBaoCao || d.TenBaoCao || 'Bao cao',
            loaiBaoCao: d.loaiBaoCao || d.LoaiBaoCao || '',
            nguoiTao: d.nguoiTao || d.NguoiTao || report?.creator || '-',
            nguoiNhanBaoCao: d.nguoiNhanBaoCao || d.NguoiNhanBaoCao || report?.recipient || '-',
            ngayTao: d.ngayTao || d.NgayTao || null,
            ngayBatDau: d.ngayBatDau || d.NgayBatDau || null,
            ngayKetThuc: d.ngayKetThuc || d.NgayKetThuc || null,
            noiDung: d.noiDung || d.NoiDung || ''
        };

        const exportRes = await fetch('/api/report/export-pdf', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(exportPayload)
        });
        if (!exportRes.ok) {
            alert('Xuất PDF thất bại.');
            return;
        }

        const blob = await exportRes.blob();
        const fileName = `${sanitizeFileName(exportPayload.tenBaoCao)}.pdf`;
        downloadBlob(blob, fileName);
    }

    function exportVisibleReportsToExcel() {
        const scoped = allReports.filter(r => !currentStatusFilter || normalizeReportStatus(r.status) === normalizeReportStatus(currentStatusFilter));
        if (!scoped.length) {
            alert('Không có dữ liệu để xuất.');
            return;
        }

        const headers = ['STT', 'Tên báo cáo', 'Loại báo cáo', 'Người tạo', 'Người nhận', 'Ngày tạo', 'Định dạng', 'Trạng thái'];
        const sanitizeCell = (value) => String(value ?? '').replace(/\r?\n/g, ' ').replace(/\t/g, ' ');
        const rows = scoped.map((r, idx) => [
            idx + 1,
            sanitizeCell(r.name),
            sanitizeCell(r.typeLabel),
            sanitizeCell(r.creator),
            sanitizeCell(r.recipient),
            sanitizeCell(r.createdDate),
            sanitizeCell(r.format),
            sanitizeCell(r.statusLabel)
        ]);

        const tsv = [headers, ...rows]
            .map(row => row.map(cell => `"${String(cell).replace(/"/g, '""')}"`).join('\t'))
            .join('\r\n');

        const blob = new Blob(['\ufeff' + tsv], { type: 'application/vnd.ms-excel;charset=utf-8;' });
        downloadBlob(blob, `DanhSachBaoCao_${new Date().toISOString().slice(0, 10)}.xls`);
    }

    function renderReportTable() {
        const tbody = document.getElementById('reportTableBody');
        const scoped = allReports.filter(r => !currentStatusFilter || normalizeReportStatus(r.status) === normalizeReportStatus(currentStatusFilter));
        document.getElementById('reportCount').textContent = `${scoped.length} báo cáo`;
        if (!scoped.length) { tbody.innerHTML = `<tr><td colspan="9" class="empty-state"><div class="empty-state-icon"><i class="bi bi-file-earmark-text"></i></div><p>Không có báo cáo phù hợp.</p></td></tr>`; return; }
        tbody.innerHTML = scoped.map((r, i) => `
            <tr>
                <td>${i + 1}</td>
                <td><span class="report-name" data-id="${r.id}">${r.name}</span></td>
                <td>${r.typeLabel || '-'}</td>
                <td>${r.creator}</td>
                <td>${r.recipient || '-'}</td>
                <td>${r.createdDate}</td>
                <td>${r.format}</td>
                <td><span class="status-badge ${getStatusBadgeClass(r.status)}">${r.statusLabel}</span></td>
                <td>
                    <div class="action-buttons">
                        <button class="action-btn btn-view" data-id="${r.id}" title="Xem"><i class="bi bi-eye"></i></button>
                        <button class="action-btn btn-download" data-id="${r.id}" title="Tải xuống"><i class="bi bi-download"></i></button>
                        <button class="action-btn delete btn-delete" data-id="${r.id}" title="Xóa"><i class="bi bi-trash"></i></button>
                        ${(roleKey === 'manager' || roleKey === 'admin') && normalizeReportStatus(r.status) === 'submitted' ? `<button class="action-btn btn-view" data-review-id="${r.id}" title="Duyệt/Từ chối"><i class="bi bi-check2-square"></i></button>` : ''}
                    </div>
                </td>
            </tr>`).join('');

        document.querySelectorAll('.btn-view[data-id]').forEach(b => b.addEventListener('click', () => viewReportDetail(Number(b.dataset.id))));
        document.querySelectorAll('.btn-download').forEach(b => b.addEventListener('click', async () => {
            const reportId = Number(b.dataset.id);
            if (!reportId) return;
            await downloadReportPdf(reportId);
        }));
        document.querySelectorAll('.btn-delete').forEach(b => b.addEventListener('click', () => {
            currentReportIdForDelete = Number(b.dataset.id);
            const report = allReports.find(x => x.id === currentReportIdForDelete);
            document.getElementById('deleteReportName').textContent = report?.name || 'này';
            openModal('deleteModal');
        }));
        document.querySelectorAll('[data-review-id]').forEach(b => b.addEventListener('click', () => {
            currentReportIdForReview = Number(b.dataset.reviewId);
            const report = allReports.find(x => x.id === currentReportIdForReview);
            document.getElementById('reviewReportName').textContent = `Báo cáo: ${report?.name || ''}`;
            document.getElementById('reviewNote').value = '';
            openModal('reviewModal');
        }));
    }

    function renderRequestList() {
        const container = document.getElementById('requestList');
        document.getElementById('requestCount').textContent = `${allRequests.length} yêu cầu`;
        if (!allRequests.length) { container.innerHTML = '<p style="color: #94a3b8; font-size: 13px; margin: 0;">Không có yêu cầu báo cáo.</p>'; return; }
        container.innerHTML = allRequests.map(r => `
            <div class="request-item">
                <h4>${r.tieuDe || 'Yêu cầu báo cáo'}</h4>
                <div class="request-meta">Ưu tiên: ${r.priority || '-'} • Hạn: ${r.hanChot ? new Date(r.hanChot).toLocaleDateString('vi-VN') : '-'} • Trạng thái: ${r.trangThai || '-'}</div>
                <div>${r.moTa || ''}</div>
                <div class="request-actions" style="margin-top:8px;">
                    <button class="btn btn-primary btn-create-from-request" data-request-id="${r.maYeuCau}">Tạo báo cáo từ yêu cầu</button>
                </div>
            </div>`).join('');
        document.querySelectorAll('.btn-create-from-request').forEach(btn => btn.addEventListener('click', () => {
            window.location.href = `/Portal/CreateReport?requestId=${btn.dataset.requestId}`;
        }));
    }

    function renderRequestRecipientOptions() {
        const select = document.getElementById('requestRecipient');
        if (!select) return;
        const candidates = (teamEmployees || [])
            .filter(x => x && x.aspNetUserId)
            .sort((a, b) => String(a.hoTen || '').localeCompare(String(b.hoTen || ''), 'vi'));
        select.innerHTML = `<option value="">Chọn nhân viên</option>` + candidates.map(x =>
            `<option value="${escapeHtml(x.aspNetUserId)}">${escapeHtml(x.hoTen || `NV #${x.maNhanVien || ''}`)}${x.email ? ` - ${escapeHtml(x.email)}` : ''}</option>`
        ).join('');
    }

    function renderHistory() {
        const historyList = document.getElementById('historyList');
        const recent = allReports.slice(0, 5);
        if (!recent.length) { historyList.innerHTML = '<p style="color: #94a3b8; font-size: 13px; margin: 0;">Không có lịch sử báo cáo nào.</p>'; return; }
        historyList.innerHTML = recent.map(r => `<div class="history-item"><div class="history-item-info"><div class="history-item-name">${r.name}</div><div class="history-item-meta">${r.creator} • ${r.createdDate}</div></div><span class="history-item-format">${r.format}</span></div>`).join('');
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function firstNonEmpty(...values) {
        for (const v of values) {
            const text = String(v ?? '').trim();
            if (text) return text;
        }
        return '';
    }

    function parseReportContent(rawContent) {
        let content = {};
        try {
            content = rawContent ? JSON.parse(rawContent) : {};
        } catch {
            content = {};
        }

        if (!content || typeof content !== 'object') {
            content = {};
        }

        const completed = firstNonEmpty(content.completed, content.completedWork, content.daHoanThanh, content.done);
        const ongoing = firstNonEmpty(content.ongoing, content.ongoingWork, content.dangThucHien, content.inProgress);
        const challenges = firstNonEmpty(content.challenges, content.khoKhan, content.vuongMac, content.issues);
        const support = firstNonEmpty(content.support, content.suggestions, content.deXuatHoTro, content.proposals);

        const selectedTasks = Array.isArray(content.selectedTasks) ? content.selectedTasks : [];
        const taskCount = selectedTasks.length;

        return { completed, ongoing, challenges, support, taskCount, selectedTasks };
    }

    function renderAnalysisTable(parsed) {
        const parts = [
            parsed.completed ? 1 : 0,
            parsed.ongoing ? 1 : 0,
            parsed.challenges ? 1 : 0,
            parsed.support ? 1 : 0
        ];
        const filledCount = parts.reduce((sum, x) => sum + x, 0);
        const completionRate = Math.round((filledCount / 4) * 100);
        const rows = [
            {
                metric: 'Mức độ đầy đủ báo cáo',
                value: `${filledCount}/4 mục (${completionRate}%)`,
                note: 'Đếm 4 mục chính: Hoàn thành / Đang thực hiện / Khó khăn / Hỗ trợ'
            },
            {
                metric: 'Số công việc được chọn',
                value: String(parsed.taskCount || 0),
                note: 'Lấy từ danh sách công việc được nhân viên chọn khi tạo báo cáo'
            },
            {
                metric: 'Mục có nội dung',
                value: `${parsed.completed ? 'Hoàn thành; ' : ''}${parsed.ongoing ? 'Đang thực hiện; ' : ''}${parsed.challenges ? 'Khó khăn; ' : ''}${parsed.support ? 'Hỗ trợ' : ''}`.trim() || 'Không có',
                note: 'Cho biết mục nào đang có dữ liệu thực tế'
            }
        ];

        document.getElementById('analysisTableBody').innerHTML = rows.map(r => `
            <tr>
                <td>${escapeHtml(r.metric)}</td>
                <td>${escapeHtml(r.value)}</td>
                <td style="white-space: normal; line-height: 1.5;">${escapeHtml(r.note)}</td>
            </tr>
        `).join('');
    }

    async function viewReportDetail(id) {
        const report = allReports.find(x => x.id === id); if (!report) return;
        currentReportIdForView = id;
        document.getElementById('detailReportName').textContent = report.name;
        document.getElementById('detailCreator').textContent = report.creator;
        document.getElementById('detailRecipient').textContent = report.recipient || '-';
        document.getElementById('detailCreatedDate').textContent = report.createdDate;
        document.getElementById('detailPeriod').textContent = report.period;
        document.getElementById('detailType').textContent = report.typeLabel;

        document.getElementById('analysisTableBody').innerHTML = '<tr><td colspan="3" style="text-align:center;color:#94a3b8;">Đang phân tích dữ liệu báo cáo...</td></tr>';
        document.getElementById('detailTableBody').innerHTML = '<tr><td colspan="5" style="text-align:center;color:#94a3b8;">Đang tải nội dung báo cáo...</td></tr>';
        openModal('viewReportModal');

        try {
            const res = await fetch(`/api/report/detail/${id}`);
            const payload = res.ok ? await res.json() : null;
            const detail = payload?.data || payload?.Data || null;
            const rawContent = detail?.noiDung || detail?.NoiDung || null;
            const parsed = parseReportContent(rawContent);
            const completed = parsed.completed;
            const ongoing = parsed.ongoing;
            const challenges = parsed.challenges;
            const support = parsed.support;
            const taskCount = parsed.taskCount;
            renderAnalysisTable(parsed);

            document.getElementById('summaryTask').textContent = taskCount > 0 ? String(taskCount) : (completed ? '1' : '-');
            const completedSections = [completed, ongoing, challenges, support].filter(Boolean).length;
            document.getElementById('summaryCompletion').textContent = `${Math.round((completedSections / 4) * 100)}%`;
            document.getElementById('summaryKpi').textContent = '-';
            document.getElementById('summaryOverdue').textContent = challenges ? '1' : '0';

            const rowsRaw = [
                { label: 'Công việc đã hoàn thành', value: completed || '-' },
                { label: 'Công việc đang thực hiện', value: ongoing || '-' },
                { label: 'Khó khăn / Vướng mắc', value: challenges || '-' },
                { label: 'Đề xuất hỗ trợ', value: support || '-' }
            ];
            const rows = rowsRaw.filter(r => r.value && r.value !== '-');

            if (!rows.length) {
                document.getElementById('detailTableBody').innerHTML = '<tr><td colspan="5" style="text-align:center;color:#94a3b8;">Báo cáo chưa có nội dung chi tiết.</td></tr>';
                return;
            }

            document.getElementById('detailTableBody').innerHTML = rows.map(r => `
                <tr>
                    <td>${escapeHtml(report.creator)}</td>
                    <td style="white-space: normal; line-height: 1.5;">${escapeHtml(r.label)}</td>
                    <td>-</td>
                    <td style="white-space: normal; line-height: 1.5;">${escapeHtml(r.value)}</td>
                    <td>Đã cập nhật</td>
                </tr>
            `).join('');
        } catch {
            document.getElementById('summaryTask').textContent = '-';
            document.getElementById('summaryCompletion').textContent = '-';
            document.getElementById('summaryKpi').textContent = '-';
            document.getElementById('summaryOverdue').textContent = '-';
            document.getElementById('analysisTableBody').innerHTML = '<tr><td colspan="3" style="text-align:center;color:#ef4444;">Không thể phân tích do lỗi tải dữ liệu.</td></tr>';
            document.getElementById('detailTableBody').innerHTML = '<tr><td colspan="5" style="text-align:center;color:#ef4444;">Không tải được nội dung báo cáo.</td></tr>';
        }
    }

    document.getElementById('btnConfirmDelete').addEventListener('click', async () => {
        if (!currentReportIdForDelete) return;
        if (await deleteBaoCaoApi(currentReportIdForDelete)) {
            closeModal('deleteModal');
            await fetchReports();
            renderReportTable();
            renderHistory();
        } else alert('Xóa báo cáo thất bại.');
    });

    document.getElementById('btnConfirmApprove').addEventListener('click', async () => {
        if (!currentReportIdForReview) return;
        const ok = await reviewReport(currentReportIdForReview, 'approve', document.getElementById('reviewNote').value || '');
        if (!ok) return alert('Duyệt báo cáo thất bại.');
        closeModal('reviewModal');
        await fetchReports();
        renderReportTable();
    });
    document.getElementById('btnConfirmReject').addEventListener('click', async () => {
        if (!currentReportIdForReview) return;
        const ok = await reviewReport(currentReportIdForReview, 'reject', document.getElementById('reviewNote').value || '');
        if (!ok) return alert('Từ chối báo cáo thất bại.');
        closeModal('reviewModal');
        await fetchReports();
        renderReportTable();
    });

    document.querySelectorAll('.status-tab').forEach(tab => tab.addEventListener('click', () => {
        document.querySelectorAll('.status-tab').forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        currentStatusFilter = tab.dataset.status || '';
        renderReportTable();
    }));

    ['reportPeriodFilter', 'reportType', 'departmentFilter'].forEach(id => {
        document.getElementById(id)?.addEventListener('change', applyReportFiltersImmediate);
    });
    document.getElementById('employeeSearch')?.addEventListener('input', () => {
        if (reportFilterInputTimer) clearTimeout(reportFilterInputTimer);
        reportFilterInputTimer = setTimeout(() => {
            applyReportFiltersImmediate();
        }, 300);
    });
    document.getElementById('btnRefresh').addEventListener('click', async () => {
        ['reportPeriodFilter', 'reportType', 'departmentFilter', 'employeeSearch'].forEach(id => document.getElementById(id).value = '');
        currentStatusFilter = '';
        document.querySelectorAll('.status-tab').forEach(t => t.classList.remove('active'));
        document.querySelector('.status-tab[data-status=""]').classList.add('active');
        await fetchReports();
        renderReportTable();
        renderHistory();
    });
    document.getElementById('btnCreateReport').addEventListener('click', () => window.location.href = '/Portal/CreateReport');
    document.getElementById('btnMyReports')?.addEventListener('click', () => window.location.href = '/Portal/ReportManagement?mine=1');
    document.getElementById('btnExportExcel').addEventListener('click', () => exportVisibleReportsToExcel());
    document.getElementById('btnDownloadFromDetail').addEventListener('click', async () => {
        if (!currentReportIdForView) {
            alert('Bạn chưa chọn báo cáo để tải.');
            return;
        }
        await downloadReportPdf(currentReportIdForView);
    });
    const btnCreateRequest = document.getElementById('btnCreateRequest');
    if (btnCreateRequest) {
        btnCreateRequest.addEventListener('click', () => {
            renderRequestRecipientOptions();
            openModal('requestModal');
        });
    }
    const btnClearRequestForm = document.getElementById('btnClearRequestForm');
    if (btnClearRequestForm) {
        btnClearRequestForm.addEventListener('click', () => {
            document.getElementById('requestRecipient').value = '';
            document.getElementById('requestTitle').value = '';
            document.getElementById('requestPriority').value = 'normal';
            document.getElementById('requestDeadline').value = '';
            document.getElementById('requestDescription').value = '';
        });
    }
    const btnSubmitRequest = document.getElementById('btnSubmitRequest');
    if (btnSubmitRequest) {
        btnSubmitRequest.addEventListener('click', async () => {
            const nguoiNhanUserId = document.getElementById('requestRecipient').value;
            const tieuDe = document.getElementById('requestTitle').value.trim();
            const priority = document.getElementById('requestPriority').value || 'normal';
            const hanChot = document.getElementById('requestDeadline').value || null;
            const moTa = document.getElementById('requestDescription').value.trim();

            if (!nguoiNhanUserId) return alert('Vui lòng chọn nhân viên nhận yêu cầu.');
            if (!tieuDe) return alert('Vui lòng nhập tiêu đề yêu cầu.');

            const result = await createReportRequest({
                nguoiNhanUserId,
                tieuDe,
                moTa,
                priority,
                hanChot
            });
            if (!result.ok) {
                return alert(result?.data?.message || 'Gửi yêu cầu thất bại.');
            }

            closeModal('requestModal');
            document.getElementById('requestRecipient').value = '';
            document.getElementById('requestTitle').value = '';
            document.getElementById('requestPriority').value = 'normal';
            document.getElementById('requestDeadline').value = '';
            document.getElementById('requestDescription').value = '';
            await fetchRequests();
            renderRequestList();
            alert('Đã gửi yêu cầu báo cáo.');
        });
    }

    document.addEventListener('DOMContentLoaded', async () => {
        await loadDepartments();
        await fetchReports();
        await fetchRequests();
        renderRequestRecipientOptions();
        renderReportTable();
        renderRequestList();
        renderHistory();
    });

