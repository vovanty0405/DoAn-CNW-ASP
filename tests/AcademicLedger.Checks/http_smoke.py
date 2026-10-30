import urllib.request, urllib.error, json, http.cookiejar, re, sys
base = sys.argv[1] if len(sys.argv)>1 else "http://localhost:5278"
for user, pwd, paths in [("admin", "admin123", ["/quan-ly-lich-hoc", "/quan-ly-lich-hoc/ma-tran", "/thoi-khoa-bieu"]), ("SV001", "123456", ["/dang-ky-hoc-phan", "/thoi-khoa-bieu", "/thoi-khoa-bieu?mode=all"]), ("GV001", "123456", ["/thoi-khoa-bieu"])]:
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    req = urllib.request.Request(base + "/api/auth/login", data=json.dumps({"Username":user,"Password":pwd}).encode(), headers={"Content-Type":"application/json"})
    r = opener.open(req)
    assert r.status == 200
    print("PASS login", user)
    for path in paths:
        r = opener.open(base + path)
        html = r.read().decode()
        assert r.status == 200 and re.search(r'class="academic(?: |")', html), path
        print("PASS render", user, path)
    if user == "admin":
        for path in ["/quan-ly-lich-hoc?configHK=HK2_2425", "/quan-ly-lich-hoc/ma-tran?viewMode=week", "/quan-ly-lich-hoc/ma-tran?viewMode=compact", "/quan-ly-lich-hoc/ma-tran?building=A&scheduleType=DayBu&date=2026-10-30"]:
            r=opener.open(base+path)
            html=r.read().decode()
            assert r.status == 200 and re.search(r'class="academic(?: |")',html)
            print("PASS upgraded screen",path)
        html=opener.open(base+"/quan-ly-lich-hoc").read().decode()
        assert re.search(r'<select name="maLop">.*?</select>',html,re.S)
        assert 'name="configHK"' in html and 'id="room-form"' in html
        print("PASS class dropdown, dynamic semester, room form rendered")
        html=opener.open(base+"/quan-ly-lich-hoc/ma-tran?viewMode=week").read().decode()
        assert html.count('class="room-matrix weekly-matrix"') == 1
        assert 'id="matrix-date"' not in html and 'name="day" data-calendar="day"' not in html
        assert 'Tuần hiện tại' in html and 'Tuần kế tiếp' in html
        assert 'Thứ Hai' in html or 'Th&#x1EE9; Hai' in html
        daily=opener.open(base+"/quan-ly-lich-hoc/ma-tran?viewMode=day").read().decode()
        assert '07:00 - 07:50' in daily and '18:20 - 19:10' in daily
        assert 'id="matrix-date"' in daily and 'Hôm nay (' in daily
        assert 'legend-swatch reserved' in daily
        print("PASS weekly room/period/day matrix, mode controls, daily times and compact legend")
    if user == "SV001":
        r = opener.open(base + "/dang-ky-hoc-phan?condition=curriculum")
        assert r.status == 200
        print("PASS curriculum filter")
        r = opener.open(base + "/dang-ky-hoc-phan?condition=faculty")
        assert r.status == 200
        print("PASS faculty filter")
        r = opener.open(base + "/dang-ky-hoc-phan?condition=major")
        assert r.status == 200
        print("PASS major filter")
        r = opener.open(base + "/dang-ky-hoc-phan?condition=class")
        assert r.status == 200
        print("PASS class filter")
        r = opener.open(base + "/quan-ly-lich-hoc")
        assert 'class="academic"' not in r.read().decode()
        print("PASS student cannot access schedule administration")
print("HTTP smoke checks passed.")
