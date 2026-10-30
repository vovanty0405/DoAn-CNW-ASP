import urllib.request, urllib.error, json, http.cookiejar, re, sys
base = sys.argv[1] if len(sys.argv)>1 else "http://localhost:5282"
def get(opener,path):
    response=opener.open(base+path)
    assert response.status==200,path
    return response.read().decode()
anon=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
home=get(anon,"/portal")
assert 'quick-actions' in home and 'article-tabs' in home and 'program-grid' in home
assert 'portal-theme' in home and 'portal-header' in home
print("PASS public portal, six quick actions, categories, programs, theme toggle")
assert get(anon,"/")==home
print("PASS anonymous home redirects to Portal")
for path in ["/portal?category=HocPhi", "/portal?search=abc", "/portal/tra-cuu-tkb", "/portal/tra-cuu-tkb?code=SV001&kind=student&week=1", "/portal/tra-cuu-tkb?code=GV001&kind=lecturer&week=1", "/quan-ly-lich-hoc/ma-tran?viewMode=week"]:
    html=get(anon,path)
    assert 'portal-header' in html
    print("PASS public screen",path)
major=re.search(r'href="(/portal/chuong-trinh/[^"?#]+)"',home)
if major:
    html=get(anon,major.group(1));assert 'portal-table' in html
    print("PASS curriculum details from existing major")
for username,password,role in [("admin","admin123","admin"),("GV001","123456","lecturer"),("SV001","123456","student")]:
    opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    req=urllib.request.Request(base+"/api/auth/login",data=json.dumps({"Username":username,"Password":password}).encode(),headers={"Content-Type":"application/json"})
    assert opener.open(req).status==200
    if role!="student":
        assert 'Biên tập nội dung' in get(opener,"/bai-viet")
        html=get(opener,"/bai-viet/them")
        assert 'contenteditable="true"' in html and 'multipart/form-data' in html and '__RequestVerificationToken' in html
        print("PASS article management and editor",role)
    else:
        html=get(opener,"/bai-viet")
        assert 'portal-header' in html and 'id="article-form"' not in html
        print("PASS student cannot access article administration")
    if role in ["student","lecturer"]:
        assert 'portal-header' in get(opener,"/")
        print("PASS signed-in home routes to Portal",role)
for path in ["/portal/bai-viet/2147483647","/portal-files/bogus.txt"]:
    try: get(anon,path);raise AssertionError("Expected 404")
    except urllib.error.HTTPError as error: assert error.code==404
    print("PASS missing or invalid resource returns 404",path)
print("Portal HTTP checks passed.")
