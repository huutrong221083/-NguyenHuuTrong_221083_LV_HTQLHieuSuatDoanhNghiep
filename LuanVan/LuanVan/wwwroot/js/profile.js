(function () {
    const btnProfileQuickExport = document.getElementById('btnProfileQuickExport');
    const profileEmployeeId = document.getElementById('profileEmployeeId');
    const btnProfileLoad = document.getElementById('btnProfileLoad');
    const profileInfo = document.getElementById('profileInfo');
    const profileSkills = document.getElementById('profileSkills');
    const profileKpiBody = document.getElementById('profileKpiBody');
    const empHoTen = document.getElementById('empHoTen');
    const empHoTenText = document.getElementById('empHoTenText');
    const empEmail = document.getElementById('empEmail');
    const empSdt = document.getElementById('empSdt');
    const empPhongBan = document.getElementById('empPhongBan');
    const empNgayVaoLam = document.getElementById('empNgayVaoLam');
    const empSkills = document.getElementById('empSkills');
    const profileEditSdt = document.getElementById('profileEditSdt');
    const profileEditDiaChi = document.getElementById('profileEditDiaChi');
    const btnSaveProfileDirect = document.getElementById('btnSaveProfileDirect');
    const profileReqEmail = document.getElementById('profileReqEmail');
    const profileReqHoTen = document.getElementById('profileReqHoTen');
    const profileReqNgaySinh = document.getElementById('profileReqNgaySinh');
    const profileReqCccd = document.getElementById('profileReqCccd');
    const profileReqLyDo = document.getElementById('profileReqLyDo');
    const btnSubmitProfileRequest = document.getElementById('btnSubmitProfileRequest');
    const profileChangeRequestBody = document.getElementById('profileChangeRequestBody');
    let profileChart;

    const escapeHtml = (value) => String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');

    const formatDate = (value) => {
        if (!value) return '-';
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? '-' : d.toLocaleDateString('vi-VN');
    };

    function exportEmployeeProfileReport() {
        const html = `
            <h3>Báo cáo cá nhân</h3>
            <p>Thời gian tạo: ${new Date().toLocaleString('vi-VN')}</p>
            <ul>
                <li>Danh mục: Hồ sơ cá nhân + kỹ năng + cập nhật mật khẩu</li>
                <li>Dữ liệu hệ thống: Công việc, KPI, Deadline, Ghi chú cá nhân</li>
            </ul>
        `;

        const popup = window.open('', '_blank');
        if (!popup) return;
        popup.document.write(`<html><head><title>Báo cáo cá nhân</title></head><body>${html}</body></html>`);
        popup.document.close();
        popup.focus();
        popup.print();
    }

    async function fetchJsonSafe(url) {
        const response = await fetch(url);
        let result;
        try {
            result = await response.json();
        } catch {
            result = { success: false, message: 'Empty or invalid response' };
        }
        return { response, result };
    }

    async function postJsonSafe(url, method, payload) {
        const response = await fetch(url, {
            method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload || {})
        });
        let result;
        try {
            result = await response.json();
        } catch {
            result = { success: false, message: 'Empty or invalid response' };
        }
        return { response, result };
    }

    function formatStatusBadge(status) {
        const s = String(status || '').trim();
        if (s === 'DaDuyet') return '<span class="badge text-bg-success">Đã duyệt</span>';
        if (s === 'TuChoi') return '<span class="badge text-bg-danger">Từ chối</span>';
        return '<span class="badge text-bg-warning">Chờ duyệt</span>';
    }

    async function loadCurrentEmployeeProfile() {
        if (!empHoTen || !empEmail || !empSdt || !empPhongBan || !empNgayVaoLam || !empSkills) return;

        try {
            const { result: jr } = await fetchJsonSafe('/nhanvien/me');
            if (!jr.success) {
                empHoTen.textContent = 'Không tải được hồ sơ.';
                if (empHoTenText) empHoTenText.textContent = '-';
                empEmail.textContent = '-';
                empSdt.textContent = '-';
                empPhongBan.textContent = '-';
                empNgayVaoLam.textContent = '-';
                empSkills.innerHTML = '<li class="text-danger">' + escapeHtml(jr.message || 'Không tải được kỹ năng') + '</li>';
                return;
            }

            const me = jr.data;
            empHoTen.textContent = me.hoTen || '-';
            if (empHoTenText) empHoTenText.textContent = me.hoTen || '-';
            empEmail.textContent = me.email || '-';
            empSdt.textContent = me.sdt || '-';
            empPhongBan.textContent = me.tenPhongBan || '-';
            empNgayVaoLam.textContent = formatDate(me.ngayVaoLam);
            if (profileEditSdt) profileEditSdt.value = me.sdt || '';

            if (me.maNhanVien) {
                const { response: dresp, result: dj } = await fetchJsonSafe(`/nhanvien/${me.maNhanVien}`);
                if (dresp.ok && dj.success && dj.data) {
                    if (profileEditDiaChi) profileEditDiaChi.value = dj.data.diaChi || '';
                    if (profileReqEmail) profileReqEmail.value = dj.data.email || '';
                    if (profileReqHoTen) profileReqHoTen.value = dj.data.hoTen || '';
                    if (profileReqCccd) profileReqCccd.value = dj.data.cccd || '';
                    if (profileReqNgaySinh && dj.data.ngaySinh) {
                        const d = new Date(dj.data.ngaySinh);
                        if (!Number.isNaN(d.getTime())) {
                            profileReqNgaySinh.value = d.toISOString().slice(0, 10);
                        }
                    }
                    const skills = dj.data.skills || [];
                    empSkills.innerHTML = Array.isArray(skills) && skills.length > 0
                        ? skills.map(s => `<li>${escapeHtml(s.tenKyNang || '-')}${s.capDo ? ' - Cap do ' + escapeHtml(String(s.capDo)) : ''}</li>`).join('')
                        : '<li>Chưa có kỹ năng được khai bao.</li>';
                    
                    // Load KPI data for current user
                    if (profileKpiBody) {
                        await loadKpiDataForEmployee(me.maNhanVien);
                    }

                    await loadMyProfileChangeRequests();
                } else {
                    empSkills.innerHTML = '<li class="text-muted">Không có dữ liệu kỹ năng.</li>';
                }
            }
        } catch (ex) {
            empHoTen.textContent = 'Lỗi tải dữ liệu';
            if (empHoTenText) empHoTenText.textContent = '-';
            empEmail.textContent = '-';
            empSdt.textContent = '-';
            empPhongBan.textContent = '-';
            empNgayVaoLam.textContent = '-';
            empSkills.innerHTML = '<li class="text-danger">Lỗi khi gọi API</li>';
        }
    }

    async function saveMyDirectProfile() {
        const payload = {
            sdt: profileEditSdt ? profileEditSdt.value : null,
            diaChi: profileEditDiaChi ? profileEditDiaChi.value : null
        };

        const { response, result } = await postJsonSafe('/nhanvien/me/profile', 'PUT', payload);
        if (!response.ok || !result.success) {
            alert(result.message || 'Không thể cập nhật hồ sơ.');
            return;
        }

        alert('Cập nhật trực tiếp thành công.');
        await loadCurrentEmployeeProfile();
    }

    async function submitProfileChangeRequest() {
        const payload = {
            email: profileReqEmail ? profileReqEmail.value : null,
            hoTen: profileReqHoTen ? profileReqHoTen.value : null,
            ngaySinh: profileReqNgaySinh && profileReqNgaySinh.value ? profileReqNgaySinh.value : null,
            cccd: profileReqCccd ? profileReqCccd.value : null,
            lyDoGui: profileReqLyDo ? profileReqLyDo.value : null
        };

        const { response, result } = await postJsonSafe('/nhanvien/me/profile-change-requests', 'POST', payload);
        if (!response.ok || !result.success) {
            alert(result.message || 'Không thể gửi yêu cầu.');
            return;
        }

        alert('Đã gửi yêu cầu cập nhật hồ sơ.');
        if (profileReqLyDo) profileReqLyDo.value = '';
        await loadMyProfileChangeRequests();
    }

    async function loadMyProfileChangeRequests() {
        if (!profileChangeRequestBody) return;

        const { response, result } = await fetchJsonSafe('/nhanvien/me/profile-change-requests');
        if (!response.ok || !result.success) {
            profileChangeRequestBody.innerHTML = `<tr><td colspan="8" class="text-danger">${escapeHtml(result.message || 'Không tải được dữ liệu')}</td></tr>`;
            return;
        }

        const rows = Array.isArray(result.data) ? result.data : [];
        if (rows.length === 0) {
            profileChangeRequestBody.innerHTML = '<tr><td colspan="8" class="text-muted">Chưa có yêu cầu.</td></tr>';
            return;
        }

        profileChangeRequestBody.innerHTML = rows.map(r => `
            <tr>
                <td>${formatStatusBadge(r.trangThai)}</td>
                <td>${escapeHtml(r.danhSachTruong || '-')}</td>
                <td>${escapeHtml(r.duLieuCuJson || '-')}</td>
                <td>${escapeHtml(r.duLieuMoiJson || '-')}</td>
                <td>${escapeHtml(r.lyDoGui || '-')}</td>
                <td>${escapeHtml(r.lyDoTuChoi || r.ghiChuDuyet || '-')}</td>
                <td>${formatDate(r.ngayTao)}</td>
                <td>${formatDate(r.ngayDuyet)}</td>
            </tr>
        `).join('');
    }

    async function loadKpiDataForEmployee(maNhanVien) {
        if (!profileKpiBody) return;
        
        try {
            const { response: kpiRes, result: kpiResult } = await fetchJsonSafe(`/kpi/nhanvien/${maNhanVien}?maKpi=1`);
            if (!kpiRes.ok || !kpiResult.success) {
                profileKpiBody.innerHTML = `<tr><td colspan="3" class="text-danger">${escapeHtml(kpiResult.message || 'Không tải được KPI')}</td></tr>`;
                return;
            }

            const rows = kpiResult.data?.lichSu12Thang ?? [];
            profileKpiBody.innerHTML = rows.length === 0
                ? '<tr><td colspan="3" class="text-muted">Không có dữ liệu KPI.</td></tr>'
                : rows.map(row => `
                    <tr>
                        <td>${row.thang}/${row.nam}</td>
                        <td>${Number(row.diem ?? 0).toFixed(2)}</td>
                        <td>${escapeHtml(row.xepLoai || '-')}</td>
                    </tr>
                `).join('');

            if (profileChart) profileChart.destroy();
            const chartCanvas = document.getElementById('profileKpiHistoryChart');
            if (chartCanvas) {
                profileChart = new Chart(chartCanvas, {
                    type: 'line',
                    data: {
                        labels: rows.map(x => `${x.thang}/${x.nam}`).reverse(),
                        datasets: [{
                            data: rows.map(x => x.diem).reverse(),
                            borderColor: '#2563EB',
                            backgroundColor: 'rgba(37,99,235,0.15)',
                            fill: true,
                            tension: 0.35
                        }]
                    },
                    options: {
                        plugins: { legend: { display: false } },
                        scales: { y: { beginAtZero: true, max: 100 } }
                    }
                });
            }
        } catch (ex) {
            if (profileKpiBody) {
                profileKpiBody.innerHTML = '<tr><td colspan="3" class="text-danger">Lỗi tải KPI</td></tr>';
            }
        }
    }

    async function loadProfile() {
        if (!profileEmployeeId || !profileInfo || !profileSkills || !profileKpiBody) return;
        if (!profileEmployeeId.value) return;

        profileInfo.innerHTML = '<div class="col-12 text-muted">Đang tải dữ liệu...</div>';
        const { response: detailRes, result: detailResult } = await fetchJsonSafe(`/nhanvien/${profileEmployeeId.value}`);

        if (!detailRes.ok || !detailResult.success) {
            profileInfo.innerHTML = `<div class="col-12 text-danger">${escapeHtml(detailResult.message || 'Không tai được ho so')}</div>`;
            return;
        }

        const nv = detailResult.data;
        profileInfo.innerHTML = `
            <div class="col-12"><strong>Ho tên:</strong> ${escapeHtml(nv.hoTen || '-')}</div>
            <div class="col-12"><strong>Email:</strong> ${escapeHtml(nv.email || '-')}</div>
            <div class="col-12"><strong>So dien thoai:</strong> ${escapeHtml(nv.sdt || '-')}</div>
            <div class="col-12"><strong>Phòng ban:</strong> ${escapeHtml(nv.tenPhongBan || '-')}</div>
            <div class="col-12"><strong>Ngay vao lam:</strong> ${formatDate(nv.ngayVaoLam)}</div>
        `;

        const skills = nv.skills ?? [];
        profileSkills.innerHTML = skills.length === 0
            ? '<li>Chưa có kỹ năng được khai bao.</li>'
            : skills.map(s => `<li>${escapeHtml(s.tenKyNang || '-')} - Cap do ${s.capDo ?? 0}</li>`).join('');

        const { response: kpiRes, result: kpiResult } = await fetchJsonSafe(`/kpi/nhanvien/${profileEmployeeId.value}?maKpi=1`);
        if (!kpiRes.ok || !kpiResult.success) {
            profileKpiBody.innerHTML = `<tr><td colspan="3" class="text-danger">${escapeHtml(kpiResult.message || 'Không tai được KPI')}</td></tr>`;
            return;
        }

        const rows = kpiResult.data?.lichSu12Thang ?? [];
        profileKpiBody.innerHTML = rows.length === 0
            ? '<tr><td colspan="3" class="text-muted">Không có dữ liệu KPI.</td></tr>'
            : rows.map(row => `
                <tr>
                    <td>${row.thang}/${row.nam}</td>
                    <td>${Number(row.diem ?? 0).toFixed(2)}</td>
                    <td>${escapeHtml(row.xepLoai || '-')}</td>
                </tr>
            `).join('');

        if (profileChart) profileChart.destroy();
        profileChart = new Chart(document.getElementById('profileKpiHistoryChart'), {
            type: 'line',
            data: {
                labels: rows.map(x => `${x.thang}/${x.nam}`).reverse(),
                datasets: [{
                    data: rows.map(x => x.diem).reverse(),
                    borderColor: '#2563EB',
                    backgroundColor: 'rgba(37,99,235,0.15)',
                    fill: true,
                    tension: 0.35
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true, max: 100 } }
            }
        });
    }

    if (btnProfileQuickExport) {
        btnProfileQuickExport.addEventListener('click', exportEmployeeProfileReport);
    }

    if (btnProfileLoad) {
        btnProfileLoad.addEventListener('click', loadProfile);
    }

    if (btnSaveProfileDirect) {
        btnSaveProfileDirect.addEventListener('click', saveMyDirectProfile);
    }

    if (btnSubmitProfileRequest) {
        btnSubmitProfileRequest.addEventListener('click', submitProfileChangeRequest);
    }

    if (profileEmployeeId) {
        // For Admin/Manager: Auto-load their own profile first
        loadCurrentEmployeeProfile();
        return;
    }

    // For Employee: Load their own profile
    loadCurrentEmployeeProfile();
})();
