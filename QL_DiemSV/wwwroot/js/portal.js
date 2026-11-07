(() => {
    'use strict';
    const button = document.getElementById('portal-theme');
    const update = () => {
        const dark = document.documentElement.classList.contains('dark');
        button.textContent = dark ? '☀' : '☾';
        button.setAttribute('aria-label', dark ? 'Chuyển sang giao diện Sáng' : 'Chuyển sang giao diện Tối');
    };
    button?.addEventListener('click', () => {
        const dark = !document.documentElement.classList.contains('dark');
        document.documentElement.classList.toggle('dark', dark);
        document.documentElement.classList.toggle('light', !dark);
        localStorage.setItem('app-theme', dark ? 'dark' : 'light');
        update();
    });
    if (button) update();

    document.getElementById('portal-logout')?.addEventListener('click', async () => {
        const response = await fetch('/api/auth/logout', { method: 'POST' });
        if (response.ok) location.assign('/portal');
    });

    // Notification Bell Widget on Portal Header
    const notifWrapper = document.getElementById('portal-notif-wrapper');
    const notifBtn = document.getElementById('portal-notif-btn');
    const notifBadge = document.getElementById('portal-notif-badge');
    const notifDropdown = document.getElementById('portal-notif-dropdown');
    const notifList = document.getElementById('portal-notif-list');
    const notifReadAll = document.getElementById('portal-notif-readall');

    function getRecipientCode() {
        const u = window.PORTAL_USER;
        if (!u || !u.isAuthenticated) return '';
        if (u.role === 'Admin') return 'ADMIN';
        return u.maSV || u.maGV || u.username || '';
    }

    function escapeHtml(str) {
        return String(str || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function formatNotifTime(isoStr) {
        if (!isoStr) return '';
        const d = new Date(isoStr);
        if (isNaN(d.getTime())) return '';
        return d.toLocaleString('vi-VN', {
            hour: '2-digit',
            minute: '2-digit',
            day: '2-digit',
            month: '2-digit',
            year: 'numeric'
        });
    }

    function resolveTargetUrl(loaiThongBao) {
        const role = window.PORTAL_USER?.role || '';
        switch (loaiThongBao) {
            case 'DiemSo':
                return '/Portal/KetQuaHocTap';
            case 'PhucKhaoDiem':
            case 'COMPLAINT_PROCESSED':
                return role === 'SinhVien' ? '/Portal/KetQuaHocTap?tab=khieu-nai' : '/khieu-nai';
            case 'COMPLAINT_NEW':
                return '/khieu-nai';
            case 'UNLOCK_REQ':
                return '/mo-khoa-diem';
            case 'UNLOCK_RES':
                return '/diem-so';
            default:
                return role === 'SinhVien' ? '/Portal/KetQuaHocTap' : '/portal';
        }
    }

    function resolveNotifIcon(loaiThongBao) {
        switch (loaiThongBao) {
            case 'DiemSo':
                return '📊';
            case 'PhucKhaoDiem':
            case 'COMPLAINT_PROCESSED':
            case 'COMPLAINT_NEW':
                return '📝';
            default:
                return '🔔';
        }
    }

    async function loadPortalNotifications() {
        const code = getRecipientCode();
        if (!code || !notifList || !notifBadge) return;

        try {
            const res = await fetch(`/api/thongbao/${encodeURIComponent(code)}`);
            if (!res.ok) return;
            const data = await res.json();
            const items = data.data || data.items || [];
            const unread = typeof data.unreadCount === 'number'
                ? data.unreadCount
                : items.filter(x => !(x.DaDoc ?? x.daDoc)).length;

            if (unread > 0) {
                notifBadge.textContent = unread > 99 ? '99+' : String(unread);
                notifBadge.hidden = false;
            } else {
                notifBadge.hidden = true;
            }

            if (items.length === 0) {
                notifList.innerHTML = '<div class="portal-notif-empty">Không có thông báo mới</div>';
                return;
            }

            notifList.innerHTML = items.map(n => {
                const id = n.MaThongBao ?? n.maThongBao ?? n.id;
                const daDoc = Boolean(n.DaDoc ?? n.daDoc);
                const loai = n.LoaiThongBao ?? n.loaiThongBao ?? '';
                const tieuDe = n.TieuDe ?? n.tieuDe ?? 'Thông báo';
                const noiDung = n.NoiDung ?? n.noiDung ?? '';
                const ngayGui = n.NgayGui ?? n.ngayGui ?? '';
                const isUnread = !daDoc;
                const icon = resolveNotifIcon(loai);
                const timeStr = formatNotifTime(ngayGui);
                return `
                    <div class="portal-notif-item ${isUnread ? 'unread' : ''}" data-notif-id="${id}" data-notif-type="${escapeHtml(loai)}">
                        <div class="portal-notif-icon">${icon}</div>
                        <div class="portal-notif-body">
                            <p class="portal-notif-title">${escapeHtml(tieuDe)}</p>
                            <p class="portal-notif-desc">${escapeHtml(noiDung)}</p>
                            <span class="portal-notif-time">${escapeHtml(timeStr)}</span>
                        </div>
                        ${isUnread ? '<span class="portal-notif-dot" aria-hidden="true"></span>' : ''}
                    </div>
                `;
            }).join('');

            notifList.querySelectorAll('.portal-notif-item').forEach(el => {
                el.addEventListener('click', async () => {
                    const id = el.getAttribute('data-notif-id');
                    const type = el.getAttribute('data-notif-type');
                    if (id) {
                        try {
                            await fetch(`/api/thongbao/${encodeURIComponent(id)}/read`, { method: 'PUT' });
                        } catch (_) {}
                    }
                    const targetUrl = resolveTargetUrl(type);
                    window.location.href = targetUrl;
                });
            });
        } catch (err) {
            console.error('Failed to load portal notifications:', err);
        }
    }

    if (notifBtn && notifDropdown) {
        notifBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            const isHidden = notifDropdown.hidden;
            notifDropdown.hidden = !isHidden;
            if (isHidden) {
                loadPortalNotifications();
            }
        });

        document.addEventListener('click', (e) => {
            if (notifWrapper && !notifWrapper.contains(e.target)) {
                notifDropdown.hidden = true;
            }
        });
    }

    if (notifReadAll) {
        notifReadAll.addEventListener('click', async (e) => {
            e.stopPropagation();
            const code = getRecipientCode();
            if (!code) return;
            try {
                await fetch(`/api/thongbao/read-all/${encodeURIComponent(code)}`, { method: 'PUT' });
                await loadPortalNotifications();
            } catch (err) {
                console.error(err);
            }
        });
    }

    if (window.PORTAL_USER?.isAuthenticated) {
        loadPortalNotifications();
        setInterval(loadPortalNotifications, 30000);
    }
})();
