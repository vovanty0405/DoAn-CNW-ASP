import urllib.request, urllib.error, http.cookiejar, subprocess, time, socket, json, re
from pathlib import Path
workspace=Path(__file__).resolve().parents[2]
base="http://localhost:5284"
with socket.socket() as probe:
    if probe.connect_ex(("127.0.0.1",5284))==0:raise RuntimeError("Port 5284 already in use; no existing process will be stopped")
log=workspace/".verification"/"semester-http.log"
with log.open("w",encoding="utf-8") as output:
    process=subprocess.Popen(["dotnet",str(workspace/".verification"/"QL_DiemSV.dll"),"--urls",base],cwd=workspace/"QL_DiemSV",stdout=output,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    try:
        for attempt in range(100):
            if process.poll() is not None:raise RuntimeError("Test server exited; inspect .verification/semester-http.log")
            try:
                urllib.request.urlopen(base+"/login",timeout=2).close();break
            except (urllib.error.URLError,TimeoutError):time.sleep(.2)
        else:raise RuntimeError("Test server did not become ready")
        client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        req=urllib.request.Request(base+"/api/auth/login",data=json.dumps({"Username":"admin","Password":"admin123"}).encode(),headers={"Content-Type":"application/json"})
        assert client.open(req).status==200
        page=client.open(base+"/quan-ly-lich-hoc").read().decode()
        block=re.search(r'<section class="panel" id="term-config">(.*?)</section>',page,re.S).group(1)
        assert 'id="term-form"' in block and 'id="term-table"' in block
        assert 'Tải cấu hình' not in block
        for name in ['maHK','ngayBatDauTuan1','soTuan','batDauDangKy','ketThucDangKy','donGiaTinChi']:
            assert 'name="'+name+'"' in block,name
        assert 'id="term-cancel"' in block and 'id="term-save"' in block
        header=re.search(r'<thead>(.*?)</thead>',block,re.S).group(1)
        assert len(re.findall(r'<th>',header))==6
        records=json.loads(re.search(r'<script id="term-config-data" type="application/json">(.*?)</script>',block,re.S).group(1))
        assert len(re.findall('data-term-row=',block))==len(records)
        assert len(re.findall('class="term-delete"',block))==len(records)
        assert all(record['code'] and record['label'] for record in records)
        assert '__RequestVerificationToken' in block
        print('PASS Razor form, semester badge/data, six-column grid, edit/delete actions and CSRF tokens')
        if records:
            edited=client.open(base+"/quan-ly-lich-hoc?configHK="+urllib.parse.quote(records[-1]['code'])).read().decode()
            assert 'value="'+records[-1]['code']+'" selected="selected"' in edited
            print('PASS GET retains explicitly selected semester')
        asset=client.open(base+'/js/term-configuration.js').read().decode()
        assert '.term-edit' in asset and '.term-delete' in asset and 'term-cancel' in asset
        print('PASS semester interaction asset served')
    finally:
        process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
        print('Stopped owned HTTP test process:',process.pid)
