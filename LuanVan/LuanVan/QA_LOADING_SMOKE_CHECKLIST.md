# QA Checklist - Loading UX Smoke Test (Pass/Fail)

Muc tieu: xac nhan trai nghiem loading theo 3 nguong truoc khi merge.

- Nguong nhanh (<120ms): khong nhap nhay spinner
- Nguong vua (~200ms): thay loading ro rang
- Nguong cham (>1s): button bi disable trong luc chay + aria-busy dung + clear trang thai sau khi xong

## Thong tin test

- Tester: ____________________
- Ngay gio: __________________
- Moi truong: __________________
- Browser: ____________________
- Build/Commit: _______________

## Buoc chuan bi chung (thuc hien 1 lan)

- [ ] Mo app va dang nhap vao Portal
- [ ] Mo DevTools Console
- [ ] Chay smoke harness tong quat:

```js
await window.UiButtonLoading.smokeTest()
```

Tieu chi dat cho smoke harness tong quat:
- [ ] quick-under-threshold = pass (showedLoading = false)
- [ ] medium-over-threshold = pass (showedLoading = true)
- [ ] slow-long-operation = pass (showedLoading = true)
- [ ] 3/3 cases pass

---

## Trang 1: Employees

Dieu huong vao trang Employees, sau do test tren 2 nhom nut dai dien.

### A. Save profile (nguong nhanh/vua - profile save)

```js
await window.UiButtonLoading.smokeTest(document.querySelector('#btnSaveEmployee'))
```

- [ ] PASS  [ ] FAIL  Ket qua: __________________
- [ ] Khong thay nhap nhay spinner o case nhanh
- [ ] Co loading ro o case vua
- [ ] Ket thuc thi bo class is-loading va bo aria-busy

### B. Export (nguong dai hon - profile export)

```js
await window.UiButtonLoading.smokeTest(document.querySelector('#btnExport'), {
  cases: [
    { name: 'quick-under-export-threshold', actionDurationMs: 150, options: { profile: 'export' }, expected: { showLoading: false } },
    { name: 'medium-over-export-threshold', actionDurationMs: 320, options: { profile: 'export' }, expected: { showLoading: true } },
    { name: 'slow-export-operation', actionDurationMs: 1200, options: { profile: 'export' }, expected: { showLoading: true } }
  ]
})
```

- [ ] PASS  [ ] FAIL  Ket qua: __________________
- [ ] O case nhanh (<250ms) khong nhap nhay loading
- [ ] O case >250ms co loading
- [ ] Trong luc loading nut bi disabled

---

## Trang 2: Projects

### A. Save Project (profile save)

```js
await window.UiButtonLoading.smokeTest(document.querySelector('#btnSaveProject'))
```

- [ ] PASS  [ ] FAIL  Ket qua: __________________
- [ ] Nhanh khong nhap nhay
- [ ] Vua/cham hien loading dung
- [ ] Clear dung state sau khi xong

### B. Delete Project (profile delete)

```js
await window.UiButtonLoading.smokeTest(document.querySelector('#btnDeleteProject'), {
  cases: [
    { name: 'quick-under-delete-threshold', actionDurationMs: 100, options: { profile: 'delete' }, expected: { showLoading: false } },
    { name: 'medium-over-delete-threshold', actionDurationMs: 250, options: { profile: 'delete' }, expected: { showLoading: true } },
    { name: 'slow-delete-operation', actionDurationMs: 1200, options: { profile: 'delete' }, expected: { showLoading: true } }
  ]
})
```

- [ ] PASS  [ ] FAIL  Ket qua: __________________
- [ ] Khi loading: disabled=true va aria-busy=true
- [ ] Ket thuc: disabled tra ve trang thai ban dau

---

## Trang 3: Tasks

### A. Export Tasks (profile export)

```js
await window.UiButtonLoading.smokeTest(document.querySelector('#btnExportTasks'), {
  cases: [
    { name: 'quick-under-export-threshold', actionDurationMs: 150, options: { profile: 'export' }, expected: { showLoading: false } },
    { name: 'medium-over-export-threshold', actionDurationMs: 320, options: { profile: 'export' }, expected: { showLoading: true } },
    { name: 'slow-export-operation', actionDurationMs: 1200, options: { profile: 'export' }, expected: { showLoading: true } }
  ]
})
```

- [ ] PASS  [ ] FAIL  Ket qua: __________________
- [ ] Export nhanh khong nhap nhay
- [ ] Export cham co loading ro

### B. Save Progress (profile save)

```js
await window.UiButtonLoading.smokeTest(document.querySelector('#btnSaveProgress'))
```

- [ ] PASS  [ ] FAIL  Ket qua: __________________
- [ ] Trong loading nut bi disable
- [ ] Sau loading phuc hoi trang thai nut

---

## Tong ket release gate

- [ ] PASS  [ ] FAIL  Employees
- [ ] PASS  [ ] FAIL  Projects
- [ ] PASS  [ ] FAIL  Tasks
- [ ] PASS  [ ] FAIL  Smoke harness tong quat 3/3

Quyet dinh merge:
- [ ] Merge duoc
- [ ] Can fix truoc khi merge

Ghi chu loi/phat hien:

1. ___________________________________________
2. ___________________________________________
3. ___________________________________________
