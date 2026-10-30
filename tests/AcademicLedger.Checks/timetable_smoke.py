import urllib.request, urllib.error, re, sys, html
base=sys.argv[1] if len(sys.argv)>1 else "http://localhost:5283"
def get(path):
    with urllib.request.urlopen(base+path) as response:
        assert response.status==200
        return response.read().decode()
def only_search(page):
    assert 'id="lookup-form"' in page
    for marker in ['name="maHK"','name="week"','id="lookup-profile"','id="timetable-results"']:
        assert marker not in page,marker
initial=get('/portal/tra-cuu-tkb');only_search(initial)
print('PASS initial screen contains only kind, code and lookup button')
invalid=get('/portal/tra-cuu-tkb?code=NOT-EXISTS&kind=student');only_search(invalid)
assert 'role="alert"' in invalid
print('PASS invalid code shows clear error without filters or grid')
for code,kind in [('SV001','student'),('GV001','lecturer')]:
    page=get('/portal/tra-cuu-tkb?code='+code+'&kind='+kind+'&week=1')
    assert 'id="lookup-profile"' in page and 'id="timetable-controls"' in page
    assert 'name="maHK"' in page and 'name="week"' in page
    assert 'id="print-timetable"' in page and 'timetable-week-nav' in page
    assert 'timetable-legend' in page and 'timetable-updated' in page
    table=re.search(r'<table class="portal-table lookup-grid enhanced-grid">(.*?)</table>',page,re.S).group(1)
    header=re.search(r'<thead>(.*?)</thead>',table,re.S).group(1)
    footer=re.search(r'<tfoot>(.*?)</tfoot>',table,re.S).group(1)
    assert len(re.findall(r'<th(?:\s|>)',header))==9
    assert len(re.findall(r'<th(?:\s|>)',footer))==9
    body=re.search(r'<tbody>(.*?)</tbody>',table,re.S).group(1)
    assert len(re.findall(r'<tr>',body))==12
    assert '07:00 - 07:50' in body and '18:20 - 19:10' in body
    assert len(re.findall('class="period-label"',body))==24
    assert 'Giảng viên đã đăng ký nghỉ dạy / Báo nghỉ' in html.unescape(page)
    print('PASS profile, controls, 12x7 grid with two period columns and legend',kind)
    full=get('/portal/tra-cuu-tkb?code='+code+'&kind='+kind+'&mode=all')
    assert 'full-term-table' in full and 'enhanced-grid' not in full
    assert 'timetable-week-nav' not in full
    print('PASS whole-semester mode renders a separate table',kind)
css=get('/css/portal-timetable.css')
assert '@media print' in css and 'A4 landscape' in css
js=get('/js/portal-timetable.js')
assert 'requestSubmit' in js and 'window.print()' in js
print('PASS automatic filters and A4 landscape print assets')
print('Timetable HTTP checks passed.')
