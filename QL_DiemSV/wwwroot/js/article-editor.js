(() => {
    'use strict';

    if (window.pdfjsLib) {
        pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
    }

    const form = document.getElementById('article-form');
    const editor = document.getElementById('rich-editor');
    const htmlArea = document.getElementById('article-html');
    if (!form || !editor || !htmlArea) return;

    htmlArea.hidden = true;
    let isSourceMode = false;

    function syncToTextarea() {
        if (!isSourceMode) {
            htmlArea.value = editor.innerHTML;
        }
    }

    // Standard formatting commands (B, I, U, S, Lists, Alignments, HR, RemoveFormat)
    document.querySelectorAll('[data-command]').forEach(button => {
        button.addEventListener('mousedown', e => e.preventDefault());
        button.addEventListener('click', () => {
            if (isSourceMode) return;
            editor.focus();
            document.execCommand(button.dataset.command, false, null);
            syncToTextarea();
        });
    });

    // Font family
    const fontFamilySelect = document.getElementById('word-font-family');
    if (fontFamilySelect) {
        fontFamilySelect.addEventListener('change', () => {
            if (isSourceMode) return;
            editor.focus();
            document.execCommand('fontName', false, fontFamilySelect.value);
            syncToTextarea();
        });
    }

    // Format Block (P, H2, H3, H4, BLOCKQUOTE)
    const formatBlockSelect = document.getElementById('word-format-block');
    if (formatBlockSelect) {
        formatBlockSelect.addEventListener('change', () => {
            if (isSourceMode) return;
            editor.focus();
            document.execCommand('formatBlock', false, formatBlockSelect.value);
            syncToTextarea();
        });
    }

    // Font Size
    const fontSizeSelect = document.getElementById('word-font-size');
    if (fontSizeSelect) {
        fontSizeSelect.addEventListener('change', () => {
            if (isSourceMode) return;
            editor.focus();
            document.execCommand('fontSize', false, fontSizeSelect.value);
            syncToTextarea();
        });
    }

    // Text Color & Highlight Color
    const foreColorInput = document.getElementById('word-fore-color');
    if (foreColorInput) {
        foreColorInput.addEventListener('input', () => {
            if (isSourceMode) return;
            editor.focus();
            document.execCommand('foreColor', false, foreColorInput.value);
            syncToTextarea();
        });
    }

    const hiliteColorInput = document.getElementById('word-hilite-color');
    if (hiliteColorInput) {
        hiliteColorInput.addEventListener('input', () => {
            if (isSourceMode) return;
            editor.focus();
            document.execCommand('hiliteColor', false, hiliteColorInput.value);
            syncToTextarea();
        });
    }

    // Insert Link
    const linkBtn = document.getElementById('editor-link');
    if (linkBtn) {
        linkBtn.addEventListener('mousedown', e => e.preventDefault());
        linkBtn.addEventListener('click', () => {
            const url = prompt('Nhập liên kết https:// hoặc đường dẫn /...:', 'https://');
            if (!url) return;
            if (!/^https?:\/\//i.test(url) && !/^\/(?![\/\\])/.test(url)) {
                alert('Liên kết không hợp lệ. Vui lòng nhập URL bắt đầu bằng https:// hoặc /.');
                return;
            }
            editor.focus();
            document.execCommand('createLink', false, url);
            syncToTextarea();
        });
    }

    // Insert Image
    const imgBtn = document.getElementById('word-insert-image');
    if (imgBtn) {
        imgBtn.addEventListener('mousedown', e => e.preventDefault());
        imgBtn.addEventListener('click', () => {
            const url = prompt('Nhập đường dẫn hình ảnh (https://... hoặc /images/...):', '/images/portal-news.svg');
            if (!url) return;
            editor.focus();
            document.execCommand('insertHTML', false, `<p><img src="${url}" alt="Hình minh họa" style="max-width:100%;" /></p>`);
            syncToTextarea();
        });
    }

    // Insert Table
    const tableBtn = document.getElementById('word-insert-table');
    if (tableBtn) {
        tableBtn.addEventListener('mousedown', e => e.preventDefault());
        tableBtn.addEventListener('click', () => {
            const rows = parseInt(prompt('Số dòng của bảng:', '4') || '4', 10);
            const cols = parseInt(prompt('Số cột của bảng:', '3') || '3', 10);
            if (isNaN(rows) || isNaN(cols) || rows < 1 || cols < 1 || rows > 30 || cols > 12) return;

            let tableHtml = '<table border="1" cellpadding="6" style="border-collapse: collapse; width: 100%; margin: 12px 0;"><thead><tr>';
            for (let c = 1; c <= cols; c++) {
                tableHtml += `<th style="padding: 8px; text-align: left;">Tiêu đề cột ${c}</th>`;
            }
            tableHtml += '</tr></thead><tbody>';
            for (let r = 1; r <= rows; r++) {
                tableHtml += '<tr>';
                for (let c = 1; c <= cols; c++) {
                    tableHtml += `<td style="padding: 8px;">Nội dung ${r}.${c}</td>`;
                }
                tableHtml += '</tr>';
            }
            tableHtml += '</tbody></table><p><br /></p>';

            editor.focus();
            document.execCommand('insertHTML', false, tableHtml);
            syncToTextarea();
        });
    }

    // Toggle HTML source view
    const toggleSourceBtn = document.getElementById('word-toggle-source');
    if (toggleSourceBtn) {
        toggleSourceBtn.addEventListener('click', () => {
            isSourceMode = !isSourceMode;
            if (isSourceMode) {
                htmlArea.value = editor.innerHTML;
                editor.hidden = true;
                htmlArea.hidden = false;
                toggleSourceBtn.classList.add('bg-primary', 'text-white');
            } else {
                editor.innerHTML = htmlArea.value;
                htmlArea.hidden = true;
                editor.hidden = false;
                toggleSourceBtn.classList.remove('bg-primary', 'text-white');
            }
        });
    }

    // Nút "📋 Chèn mẫu Thông báo Nghỉ - Bù"
    const tplNghiBuBtn = document.getElementById('btn-template-nghibu');
    if (tplNghiBuBtn) {
        tplNghiBuBtn.addEventListener('click', () => {
            const titleEl = document.getElementById('editor-input-title');
            const catEl = document.getElementById('editor-input-category');
            const unitEl = document.getElementById('editor-input-unit');

            if (catEl) catEl.value = 'NghiBu';
            if (titleEl && !titleEl.value.trim()) {
                titleEl.value = 'Thông báo nghỉ lớp [Tên môn học] ([Mã LHP]) ngày dd/MM/yyyy';
            }

            const unitName = (unitEl && unitEl.value.trim()) ? unitEl.value.trim() : 'Phòng Đào tạo Đại học';
            const templateHtml = `
<p style="color: #cbd5e1; margin-bottom: 12px;"><strong>${unitName}</strong> thông báo đến sinh viên về việc <strong>nghỉ học / học bù</strong> của lớp học phần với thông tin chi tiết như sau:</p>
<table style="width: 100%; border-collapse: collapse; background-color: rgba(30, 41, 59, 0.5); border: 1px solid #334155; margin-bottom: 16px;">
    <tbody>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="width: 28%; padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Cán bộ giảng dạy (CBGD)</td>
            <td style="padding: 10px 14px; color: #f8fafc; font-weight: 600;">[Nhập Họ tên Giảng viên, VD: TS. Nguyễn Văn An]</td>
        </tr>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Khoa / Bộ môn</td>
            <td style="padding: 10px 14px; color: #f8fafc;">[Khoa Công nghệ Thông tin]</td>
        </tr>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Môn học</td>
            <td style="padding: 10px 14px; color: #f8fafc; font-weight: 600;">[Nhập Tên môn học, VD: Tổ chức và cấu trúc máy tính]</td>
        </tr>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Lớp học phần</td>
            <td style="padding: 10px 14px; color: #38bdf8; font-weight: 600;">[Nhập Mã LHP, VD: IT010.R12 / LHP002]</td>
        </tr>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Phòng học</td>
            <td style="padding: 10px 14px; color: #f8fafc;">Phòng [A102]</td>
        </tr>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Thời gian</td>
            <td style="padding: 10px 14px; color: #f8fafc;">Tiết bắt đầu: <strong>[1]</strong> — Tiết kết thúc: <strong>[5]</strong></td>
        </tr>
        <tr style="border-bottom: 1px solid #334155;">
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Thứ / Ngày</td>
            <td style="padding: 10px 14px; color: #f43f5e; font-weight: 600;">Thứ [...], ngày dd/MM/yyyy</td>
        </tr>
        <tr>
            <td style="padding: 10px 14px; color: #94a3b8; font-weight: 500; background-color: rgba(15, 23, 42, 0.4);">Ghi chú / Kế hoạch bù</td>
            <td style="padding: 10px 14px; color: #cbd5e1;">Lịch học bù sẽ được cập nhật trên Thời khóa biểu sinh viên.</td>
        </tr>
    </tbody>
</table>
<p style="color: #94a3b8; margin-top: 10px; font-style: italic;">Đề nghị sinh viên thuộc lớp học phần trên lưu ý theo dõi lịch học bù trên Cổng thông tin Đào tạo.</p>`;

            if (isSourceMode) {
                htmlArea.value = templateHtml.trim();
            } else {
                editor.innerHTML = templateHtml.trim();
                syncToTextarea();
            }
        });
    }

    // Nút "💰 Chèn mẫu Thông báo Học phí"
    const tplHocPhiBtn = document.getElementById('btn-template-hocphi');
    if (tplHocPhiBtn) {
        tplHocPhiBtn.addEventListener('click', () => {
            const titleEl = document.getElementById('editor-input-title');
            const catEl = document.getElementById('editor-input-category');
            const unitEl = document.getElementById('editor-input-unit');

            if (catEl) catEl.value = 'HocPhi';
            if (unitEl) unitEl.value = 'Phòng Kế hoạch - Tài chính';
            if (titleEl && !titleEl.value.trim()) {
                titleEl.value = 'Thông báo về việc thu học phí Học kỳ [...] năm học 2026 - 2027';
            }

            const templateHtml = `
<h3>1. Thời gian và đối tượng thu học phí</h3>
<p>Phòng Kế hoạch - Tài chính thông báo đến toàn thể sinh viên hệ Đại học chính quy về lịch thu học phí học kỳ:</p>
<ul>
    <li><strong>Thời gian đóng học phí:</strong> Từ ngày <strong>dd/MM/yyyy</strong> đến hết ngày <strong>dd/MM/yyyy</strong>.</li>
    <li><strong>Đơn giá tín chỉ:</strong> 635.000 VNĐ / tín chỉ.</li>
</ul>
<h3>2. Hướng dẫn thanh toán</h3>
<p>Sinh viên đăng nhập vào Cổng thông tin Đào tạo mục <strong>Đăng ký học phần</strong> để kiểm tra công nợ học phí và thực hiện thanh toán trực tuyến đúng hạn.</p>`;

            if (isSourceMode) {
                htmlArea.value = templateHtml.trim();
            } else {
                editor.innerHTML = templateHtml.trim();
                syncToTextarea();
            }
        });
    }

    editor.addEventListener('input', syncToTextarea);

    form.addEventListener('submit', event => {
        if (!isSourceMode) {
            htmlArea.value = editor.innerHTML;
        } else {
            editor.innerHTML = htmlArea.value;
        }
        if (!editor.textContent.trim()) {
            event.preventDefault();
            editor.focus();
            alert('Vui lòng nhập nội dung bài viết.');
        }
    });

    // Global helper: जब người dùng chọn file PDF trên form Edit
    window.onEditorAttachmentSelected = function (input) {
        const file = input.files && input.files[0];
        const statusEl = document.getElementById('editor-pdf-extract-status');
        if (!file || !statusEl) return;
        statusEl.textContent = `Đã chọn tệp: "${file.name}" (${Math.ceil(file.size / 1024)} KB). Bấm "⚡ Trích xuất chữ từ PDF vào Editor" nếu muốn đưa văn bản trong file vào khung soạn thảo.`;
    };

    // Global helper: Trích xuất chữ từ file PDF vào trình soạn thảo Word
    window.extractSelectedPdfToEditor = async function () {
        const fileInput = document.getElementById('editor-file-upload');
        const statusEl = document.getElementById('editor-pdf-extract-status');
        const titleInput = document.getElementById('editor-input-title');
        const file = fileInput && fileInput.files && fileInput.files[0];

        if (!file) {
            alert('Vui lòng chọn một file .pdf hoặc .txt ở ô bên cạnh trước khi trích xuất.');
            return;
        }

        const ext = file.name.split('.').pop().toLowerCase();
        if (ext === 'txt') {
            const text = await file.text();
            const html = text.split(/\r?\n/).filter(l => l.trim()).map(l => `<p>${l}</p>`).join('\n');
            editor.innerHTML = html;
            syncToTextarea();
            if (statusEl) statusEl.textContent = `✓ Đã nạp nội dung từ file văn bản "${file.name}" vào trình soạn thảo.`;
            return;
        }

        if (ext !== 'pdf' || !window.pdfjsLib) {
            alert('Chức năng trích xuất văn bản trực tiếp áp dụng cho file .pdf hoặc .txt. File của bạn vẫn được lưu đính kèm vào bài viết.');
            return;
        }

        try {
            if (statusEl) statusEl.textContent = '⏳ Đang đọc và trích xuất văn bản từ file PDF...';
            const arrayBuffer = await file.arrayBuffer();
            const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
            const paragraphs = [];
            let firstHeading = '';

            for (let i = 1; i <= pdf.numPages; i++) {
                const page = await pdf.getPage(i);
                const content = await page.getTextContent();
                let currentLine = '';
                let lastY = null;

                for (const item of content.items) {
                    const y = item.transform ? Math.round(item.transform[5]) : null;
                    if (lastY !== null && y !== null && Math.abs(y - lastY) > 6) {
                        if (currentLine.trim()) {
                            if (!firstHeading) firstHeading = currentLine.trim();
                            paragraphs.push(`<p>${currentLine.trim()}</p>`);
                        }
                        currentLine = item.str;
                    } else {
                        currentLine += (currentLine && !currentLine.endsWith(' ') ? ' ' : '') + item.str;
                    }
                    if (y !== null) lastY = y;
                }
                if (currentLine.trim()) {
                    if (!firstHeading) firstHeading = currentLine.trim();
                    paragraphs.push(`<p>${currentLine.trim()}</p>`);
                }
            }

            if (titleInput && !titleInput.value.trim() && firstHeading) {
                titleInput.value = firstHeading.slice(0, 240);
            }

            const resultHtml = paragraphs.length > 0
                ? paragraphs.join('\n')
                : `<p>Đính kèm tài liệu PDF: <strong>${file.name}</strong>.</p>`;

            editor.innerHTML = resultHtml;
            syncToTextarea();
            if (statusEl) statusEl.textContent = `✓ Đã trích xuất thành công ${pdf.numPages} trang từ file PDF "${file.name}" vào trình soạn thảo!`;
        } catch (e) {
            if (statusEl) statusEl.textContent = `⚠️ Không thể trích xuất tự động từ file PDF này, nhưng file vẫn được đính kèm vào bài viết.`;
        }
    };

    // Global helper: Xuất nội dung đang soạn thảo ra file PDF
    window.exportEditorContentToPdf = function () {
        if (typeof html2pdf === 'undefined') {
            window.print();
            return;
        }
        const title = document.getElementById('editor-input-title')?.value || 'Thông báo đào tạo';
        const unit = document.getElementById('editor-input-unit')?.value || 'Phòng Đào tạo Đại học';
        const contentHtml = isSourceMode ? htmlArea.value : editor.innerHTML;

        document.getElementById('editor-pdf-unit').textContent = unit;
        document.getElementById('editor-pdf-title').textContent = title;
        document.getElementById('editor-pdf-content').innerHTML = contentHtml;

        const staging = document.getElementById('editor-pdf-staging');
        const opt = {
            margin: [12, 12, 12, 12],
            filename: 'ThongBao_BaiViet.pdf',
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { scale: 2, useCORS: true },
            jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
        };
        html2pdf().set(opt).from(staging).save();
    };
})();
