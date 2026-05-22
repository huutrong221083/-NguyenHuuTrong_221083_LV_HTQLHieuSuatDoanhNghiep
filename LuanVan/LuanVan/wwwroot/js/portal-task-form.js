window.PortalTaskForm = (function() {
    let cfg = null;
    let state = { currentProjectId: null, editingTaskId: null };

    const DEFAULT_PRIORITIES = [
        { id: 1, name: 'Thấp' },
        { id: 2, name: 'Trung bình' },
        { id: 3, name: 'Cao' }
    ];

    const DEFAULT_DIFFICULTIES = [
        { id: 1, name: 'Dễ' },
        { id: 2, name: 'Trung bình' },
        { id: 3, name: 'Khó' }
    ];

    async function requestJson(url, options = {}) {
        try {
            const res = await fetch(url, options);
            const txt = await res.text();

            // If response is JSON, parse it. Otherwise return text as message.
            const contentType = res.headers.get('content-type') || '';
            let data = null;
            if (contentType.includes('application/json')) {
                try { data = txt ? JSON.parse(txt) : null; } catch (e) { data = null; }
            }

            if (!res.ok) {
                const message = data?.message || txt || `HTTP ${res.status}`;
                return { success: false, message, data };
            }

            if (data && typeof data.success === 'boolean') return data;
            if (data !== null) return { success: true, data };

            // non-json but 200 OK
            return { success: true, data: txt };
        } catch (err) {
            return { success: false, message: err.message };
        }
    }

    function by(id) { return document.getElementById(id); }

    function fillSelectOptions(selectId, items, emptyLabel = '') {
        const select = by(selectId);
        if (!select) return;
        const head = emptyLabel ? `<option value="">${emptyLabel}</option>` : '';
        const options = (items || [])
            .map(item => `<option value="${item.id}">${item.name}</option>`)
            .join('');
        select.innerHTML = head + options;
    }

    async function loadLookups(projectId = null) {
        const ids = cfg.ids;
        const [employeesRes, teamsRes, deptsRes] = await Promise.all([
            requestJson('/nhanvien?page=1&size=200').catch(() => ({ data:{items:[]} })),
            requestJson('/api/nhom').catch(() => ({ data:[] })),
            requestJson('/phongban').catch(() => ({ data:[] }))
        ]);

        const allEmployees = (employeesRes.data?.items) || [];
        const allTeams = teamsRes.data || teamsRes.data?.items || [];
        const allDepts = deptsRes.data || deptsRes.data?.items || [];
        const doKhos = DEFAULT_DIFFICULTIES;
        const doUuTiens = DEFAULT_PRIORITIES;

        let employees = allEmployees;
        let teams = allTeams;
        let depts = allDepts;

        if (projectId) {
            const [projectEmployeesRes, projectTeamsRes, projectDeptsRes] = await Promise.all([
                requestJson(`/duan/${projectId}/nhanvien`).catch(() => ({ data: [] })),
                requestJson(`/duan/${projectId}/nhom`).catch(() => ({ data: [] })),
                requestJson(`/duan/${projectId}/phongban`).catch(() => ({ data: [] }))
            ]);

            const projectEmployees = projectEmployeesRes.data || [];
            const projectTeams = projectTeamsRes.data || [];
            const projectDepts = projectDeptsRes.data || [];

            const allowedDeptIds = new Set(projectDepts.map(x => Number(x.maPhongBan)).filter(Number.isFinite));
            const allowedEmployeeMap = new Map();

            projectEmployees.forEach(x => {
                const id = Number(x.maNhanVien);
                if (Number.isFinite(id) && id > 0) {
                    allowedEmployeeMap.set(id, {
                        maNhanVien: id,
                        hoTen: x.hoTen || `NV ${id}`
                    });
                }
            });

            const deptEmployeeBatches = await Promise.all(
                Array.from(allowedDeptIds).map(deptId =>
                    requestJson(`/phongban/${deptId}/nhanvien`).catch(() => ({ data: [] }))
                )
            );

            deptEmployeeBatches.forEach(batch => {
                const rows = batch.data || [];
                rows.forEach(x => {
                    const id = Number(x.maNhanVien);
                    if (Number.isFinite(id) && id > 0) {
                        allowedEmployeeMap.set(id, {
                            maNhanVien: id,
                            hoTen: x.hoTen || `NV ${id}`
                        });
                    }
                });
            });

            const allowedEmployeeIds = new Set(Array.from(allowedEmployeeMap.keys()));
            const projectTeamIds = new Set(projectTeams.map(x => Number(x.maNhom)).filter(Number.isFinite));
            const allowedTeamIds = new Set(projectTeamIds);

            const teamDetails = await Promise.all(
                allTeams
                    .filter(x => !projectTeamIds.has(Number(x.maNhom)))
                    .map(x => requestJson(`/api/nhom/${x.maNhom}`).then(r => ({ team: x, detail: r.data })).catch(() => ({ team: x, detail: null })))
            );

            teamDetails.forEach(item => {
                const team = item.team;
                const detail = item.detail;
                if (!detail) return;

                const memberIds = new Set((detail.thanhViens || []).map(m => Number(m.maNhanVien)).filter(Number.isFinite));
                const leaderId = Number(detail.truongNhom || team.truongNhom || 0);
                if (leaderId > 0) {
                    memberIds.add(leaderId);
                }

                if (!memberIds.size) return;

                const isAllowed = Array.from(memberIds).every(id => allowedEmployeeIds.has(id));
                if (isAllowed) {
                    allowedTeamIds.add(Number(team.maNhom));
                }
            });

            employees = Array.from(allowedEmployeeMap.values()).sort((a, b) => String(a.hoTen || '').localeCompare(String(b.hoTen || ''), 'vi'));
            depts = allDepts.filter(x => allowedDeptIds.has(Number(x.maPhongBan)));
            teams = allTeams.filter(x => allowedTeamIds.has(Number(x.maNhom)));
        }

        if (by(ids.employee)) by(ids.employee).innerHTML = '<option value="">Chọn nhân viên</option>' + employees.map(x => `<option value="${x.maNhanVien}">${x.hoTen}</option>`).join('');
        if (by(ids.team)) by(ids.team).innerHTML = '<option value="">Chọn nhóm</option>' + teams.map(x => `<option value="${x.maNhom}">${x.tenNhom}</option>`).join('');
        if (by(ids.department)) by(ids.department).innerHTML = '<option value="">Chọn phòng ban</option>' + depts.map(x => `<option value="${x.maPhongBan}">${x.tenPhongBan}</option>`).join('');
        fillSelectOptions(ids.priority, doUuTiens);
        fillSelectOptions(ids.difficulty, doKhos);
    }

    async function loadProjectsInto(projectSelectId) {
        try {
            const res = await requestJson('/duan?page=1&size=200');
            const items = res.data?.items || [];
            if (by(projectSelectId)) by(projectSelectId).innerHTML = items.map(p => `<option value="${p.maDuAn}">${p.tenDuAn}</option>`).join('');
        } catch (e) { /* ignore */ }
    }

    async function loadParentsForProject(projectId) {
        const ids = cfg.ids;
        try {
            const res = await requestJson(`/duan/${projectId}/tasks`);
            const parents = res.data || [];
            if (by(ids.parent)) by(ids.parent).innerHTML = '<option value="">(Không)</option>' + parents.map(t => `<option value="${t.maCongViec}">${t.tenCongViec}</option>`).join('');
        } catch (e) { if (by(cfg.ids.parent)) by(cfg.ids.parent).innerHTML = '<option value="">(Không)</option>'; }
    }

    async function openForProject(projectId) {
        if (!cfg && window.__PortalTaskFormDefaultIds) {
            init({ ids: window.__PortalTaskFormDefaultIds, onSaved: window.__PortalTaskFormOnSaved });
        }
        if (!cfg || !cfg.ids) {
            throw new Error('PortalTaskForm is not initialized.');
        }
        state.currentProjectId = projectId;
        if (by(cfg.ids.wrap)) by(cfg.ids.wrap).classList.remove('d-none');
        if (by(cfg.ids.project)) {
            await loadProjectsInto(cfg.ids.project);
            by(cfg.ids.project).value = String(projectId);
        }
        await loadLookups(projectId);
        await loadParentsForProject(projectId);
    }

    async function openTaskForm(taskId = null) {
        state.editingTaskId = taskId;
        if (by(cfg.ids.wrap)) by(cfg.ids.wrap).classList.remove('d-none');
        if (!taskId) {
            // reset
            ['name','desc','parent','status','start','deadline','employee','team','department','priority','difficulty'].forEach(k => {
                const el = by(cfg.ids[k]); if (el) el.value = '';
            });
            if (by(cfg.ids.status)) by(cfg.ids.status).value = '1';
            if (by(cfg.ids.priority)) by(cfg.ids.priority).value = '2';
            if (by(cfg.ids.difficulty)) by(cfg.ids.difficulty).value = '2';
            return;
        }
        const res = await requestJson(`/congviec/${taskId}`);
        const detail = res.data || {};
        if (by(cfg.ids.name)) by(cfg.ids.name).value = detail.tenCongViec || '';
        if (by(cfg.ids.desc)) by(cfg.ids.desc).value = detail.moTa || '';
        if (by(cfg.ids.parent)) by(cfg.ids.parent).value = detail.maCongViecCha ? String(detail.maCongViecCha) : '';
        if (by(cfg.ids.status)) by(cfg.ids.status).value = String(detail.maTrangThai || 1);
        if (by(cfg.ids.priority)) by(cfg.ids.priority).value = String(detail.maDoUuTien || 2);
        if (by(cfg.ids.difficulty)) by(cfg.ids.difficulty).value = String(detail.maDoKho || 2);
        if (by(cfg.ids.start)) by(cfg.ids.start).value = detail.ngayBatDau ? new Date(detail.ngayBatDau).toISOString().slice(0,10) : '';
        if (by(cfg.ids.deadline)) by(cfg.ids.deadline).value = detail.hanHoanThanh ? new Date(detail.hanHoanThanh).toISOString().slice(0,10) : '';
        if (by(cfg.ids.employee)) by(cfg.ids.employee).value = detail.nguoiDuocGiao?.[0]?.maNhanVien ? String(detail.nguoiDuocGiao[0].maNhanVien) : '';
    }

    async function saveTask() {
        const ids = cfg.ids;
        const isEdit = Boolean(state.editingTaskId);
        const payload = {
            tenCongViec: by(ids.name).value.trim(),
            moTa: by(ids.desc).value.trim(),
            maDuAn: Number(by(ids.project).value || state.currentProjectId || 0) || null,
            maCongViecCha: by(ids.parent).value ? Number(by(ids.parent).value) : null,
            maDoKho: Number(by(ids.difficulty).value || 0) || null,
            maDoUuTien: Number(by(ids.priority).value || 0) || null,
            maTrangThai: Number(by(ids.status).value || 1),
            ngayBatDau: by(ids.start).value || null,
            deadline: by(ids.deadline).value || null,
            diemCongViec: 0
        };

        if (!payload.tenCongViec) return setResult(false, 'Tên công việc là bắt buộc.');
        if (!payload.maDuAn) return setResult(false, 'Vui lòng chọn dự án.');

        const url = isEdit ? `/congviec/${state.editingTaskId}` : '/congviec';
        const method = isEdit ? 'PUT' : 'POST';
        const res = await requestJson(url, { method, headers:{'Content-Type':'application/json'}, body: JSON.stringify(payload) });
        if (!res.success) return setResult(false, res.message || 'Không thể lưu công việc.');

        const newTaskId = state.editingTaskId || (res.data?.maCongViec || res.data?.data?.maCongViec || null);

        // assignments
        const assignmentErrors = [];
        if (newTaskId && by(ids.employee) && by(ids.employee).value) {
            const assignEmployeeRes = await requestJson('/phancong/nhanvien', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ maCongViec: newTaskId, maNhanVien: Number(by(ids.employee).value) })
            });
            if (!assignEmployeeRes.success) {
                assignmentErrors.push(assignEmployeeRes.message || 'Không thể giao nhân viên theo phạm vi dự án.');
            }
        }
        if (newTaskId && by(ids.team) && by(ids.team).value) {
            const assignTeamRes = await requestJson(`/congviec/${newTaskId}/assignments/nhom`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ maNhom: Number(by(ids.team).value) })
            });
            if (!assignTeamRes.success) {
                assignmentErrors.push(assignTeamRes.message || 'Không thể giao nhóm theo phạm vi dự án.');
            }
        }
        if (newTaskId && by(ids.department) && by(ids.department).value) {
            const assignDepartmentRes = await requestJson(`/congviec/${newTaskId}/assignments/phongban`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ maPhongBan: Number(by(ids.department).value) })
            });
            if (!assignDepartmentRes.success) {
                assignmentErrors.push(assignDepartmentRes.message || 'Không thể giao phòng ban theo phạm vi dự án.');
            }
        }

        if (assignmentErrors.length) {
            return setResult(false, assignmentErrors[0]);
        }

        setResult(true, 'Đã lưu công việc.');
        if (typeof cfg.onSaved === 'function') cfg.onSaved(newTaskId);
        // reset
        state.editingTaskId = null;
        if (by(ids.wrap)) by(ids.wrap).classList.add('d-none');
    }

    function setResult(ok, message) {
        const el = by(cfg.ids.result);
        if (!el) return;
        el.className = `small mt-2 ${ok ? 'text-success' : 'text-danger'}`;
        el.textContent = message;
    }

    function init(options = {}) {
        cfg = options || {};
        cfg.ids = cfg.ids || {};
        // wire save button
        const saveBtn = by(cfg.ids.save);
        if (saveBtn) saveBtn.addEventListener('click', saveTask);
        // expose functions
        window.openTaskForm = openTaskForm;
        window.saveTask = saveTask;
        // allow external override of bindings
        if (cfg.overrideBindings && cfg.externalSaveButtonId) {
            const ext = by(cfg.externalSaveButtonId);
            if (ext && ext.parentNode) {
                // replace node with clone to remove existing listeners
                const clone = ext.cloneNode(true);
                ext.parentNode.replaceChild(clone, ext);
                clone.addEventListener('click', saveTask);
            }
        }
    }

    return { init, openForProject, openTaskForm, saveTask };
})();
