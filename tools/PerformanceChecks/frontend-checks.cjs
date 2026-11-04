const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');

function page(name, respond) {
    const markup = fs.readFileSync(`QL_DiemSV/Views/Home/${name}.cshtml`, 'utf8');
    const script = [...markup.matchAll(/<script>([\s\S]*?)<\/script>/g)].at(-1)[1]
        .replace(/\n\s{8}loadData\(\);\s*$/, '');
    const elements = new Map();
    const get = id => {
        if (!elements.has(id)) elements.set(id, { value: '', innerHTML: '', textContent: '', classList: { add() {}, remove() {} } });
        return elements.get(id);
    };
    const calls = [];
    let exported;
    const context = vm.createContext({
        URLSearchParams, setTimeout, clearTimeout, console,
        document: { getElementById: get, addEventListener() {} },
        lucide: { createIcons() {} },
        showToast(message, type) { if (type === 'error') throw new Error(message); },
        apiFetch: async url => { calls.push(url); return respond(new URL(url, 'http://localhost')); },
        XLSX: { utils: { json_to_sheet(rows) { exported = rows; return {}; }, book_new() { return {}; }, book_append_sheet() {} }, writeFile() {} }
    });
    vm.runInContext(script, context);
    return { get, calls, context, exported: () => exported };
}

(async () => {
    const courses = page('LopHocPhan', url => ({ data: [], total: 350, totalPages: 35,
        page: Number(url.searchParams.get('page')), maHK: 'HK1_2627' }));
    courses.get('lhp-hk').value = 'HK1_2627';
    courses.get('lhp-search').value = 'LHP test';
    await vm.runInContext('renderLhpTable(true)', courses.context);
    await vm.runInContext('lhpCurrentPage = 2; renderLhpTable()', courses.context);
    const courseUrl = new URL(courses.calls.at(-1), 'http://localhost');
    assert.equal(courseUrl.searchParams.get('page'), '2');
    assert.equal(courseUrl.searchParams.get('maHK'), 'HK1_2627');
    assert.equal(courseUrl.searchParams.get('search'), 'LHP test');
    assert.match(courses.get('lhp-pagination-info').textContent, /350/);
    console.log('PASS course filters/page 2 use server metadata');

    const students = page('SinhVien', url => ({
        data: [{ MaSV: `SV${url.searchParams.get('page')}`, HoTen: 'Sinh viên thử' }],
        total: 350, totalPages: url.searchParams.get('pageSize') === '100' ? 2 : 35,
        page: Number(url.searchParams.get('page'))
    }));
    students.get('sv-lop').value = 'DH26TH';
    students.get('sv-search').value = 'test';
    await vm.runInContext('svCurrentPage = 2; renderSvTable()', students.context);
    const studentUrl = new URL(students.calls.at(-1), 'http://localhost');
    assert.equal(studentUrl.searchParams.get('page'), '2');
    assert.equal(studentUrl.searchParams.get('maLop'), 'DH26TH');
    assert.match(students.get('sv-pagination-info').textContent, /350/);
    await vm.runInContext('exportSinhVienExcel()', students.context);
    assert.equal(students.exported().length, 2, 'Export must collect all server pages, not only visible rows');
    assert(students.calls.every(url => new URL(url, 'http://localhost').searchParams.get('maLop') === 'DH26TH'));
    console.log('PASS student filters/page 2 and full filtered export');
})().catch(error => { console.error(error); process.exitCode = 1; });
