// ══════════════════════════════════════════════════════════════
// ARTICLE.JS — Thông báo, bình luận, SafeWiki, cỡ chữ
// ══════════════════════════════════════════════════════════════

const ArticleApp = {
    // ── NOTIFICATIONS ──
    initNotifications() {
        const bell = document.getElementById('notif-bell');
        const dropdown = document.getElementById('notif-dropdown');
        if (!bell || !dropdown) return;

        bell.addEventListener('click', (e) => {
            e.stopPropagation();
            dropdown.classList.toggle('show');
            if (dropdown.classList.contains('show')) {
                this.loadNotifications();
            }
        });

        document.addEventListener('click', (e) => {
            if (!dropdown.contains(e.target) && e.target !== bell) {
                dropdown.classList.remove('show');
            }
        });

        // Polling mỗi 30s
        this.loadNotificationCount();
        setInterval(() => this.loadNotificationCount(), 30000);
    },

    async loadNotificationCount() {
        try {
            const res = await fetch('/Article/GetNotifications');
            if (!res.ok) return;
            const data = await res.json();
            const badge = document.getElementById('notif-count');
            if (badge) {
                badge.textContent = data.unreadCount;
                badge.style.display = data.unreadCount > 0 ? 'flex' : 'none';
            }
        } catch (e) { }
    },

    async loadNotifications() {
        try {
            const res = await fetch('/Article/GetNotifications');
            if (!res.ok) return;
            const data = await res.json();
            const list = document.getElementById('notif-list');
            if (!list) return;

            if (data.items.length === 0) {
                list.innerHTML = '<div class="notif-empty">Chưa có thông báo nào</div>';
                return;
            }

            list.innerHTML = data.items.map(n => `
                <div class="notif-item ${n.isRead ? '' : 'unread'}" onclick="ArticleApp.readNotification(${n.id}, ${n.articleId})">
                    <div class="notif-item__title">${n.title}</div>
                    <div class="notif-item__msg">${n.content}</div>
                    <div class="notif-item__time">${n.createdAt}</div>
                </div>
            `).join('');
        } catch (e) { }
    },

    async readNotification(id, articleId) {
        try {
            await fetch(`/Article/MarkNotificationRead/${id}`, { method: 'POST' });
            if (articleId) {
                window.location.href = `/Article/Details/${articleId}`;
            } else {
                this.loadNotifications();
                this.loadNotificationCount();
            }
        } catch (e) { }
    },

    async markAllRead() {
        try {
            await fetch('/Article/MarkAllRead', { method: 'POST' });
            this.loadNotifications();
            this.loadNotificationCount();
        } catch (e) { }
    },

    // ── COMMENTS ──
    initComments() {
        const form = document.getElementById('comment-form');
        if (!form) return;

        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const textarea = form.querySelector('textarea');
            const content = textarea.value.trim();
            if (!content) return;

            const articleId = form.dataset.articleId;
            const btn = form.querySelector('button');
            btn.disabled = true;
            btn.textContent = 'Đang gửi...';

            try {
                const res = await fetch('/Article/AddComment', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ articleId: parseInt(articleId), content })
                });

                if (res.status === 401) {
                    alert('Vui lòng đăng nhập để bình luận');
                    return;
                }

                const data = await res.json();
                const list = document.getElementById('comment-list');
                const initials = data.userName.charAt(0).toUpperCase();
                const html = `
                    <div class="art-comment-item">
                        <div class="art-comment-avatar">${initials}</div>
                        <div class="art-comment-body">
                            <span class="art-comment-name">${data.userName}</span>
                            <span class="art-comment-time">${data.createdAt}</span>
                            <div class="art-comment-text">${data.content}</div>
                        </div>
                    </div>`;
                list.insertAdjacentHTML('afterbegin', html);
                textarea.value = '';

                const countEl = document.getElementById('comment-count');
                if (countEl) countEl.textContent = parseInt(countEl.textContent) + 1;
            } catch (e) {
                alert('Có lỗi xảy ra, vui lòng thử lại');
            } finally {
                btn.disabled = false;
                btn.textContent = 'Gửi bình luận';
            }
        });
    },

    // ── FONT SIZE ──
    initFontControls() {
        const btns = document.querySelectorAll('.art-font-btn');
        const content = document.querySelector('.art-detail__content');
        if (!btns.length || !content) return;

        btns.forEach(btn => {
            btn.addEventListener('click', () => {
                btns.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                content.className = 'art-detail__content';
                if (btn.dataset.size === 'large') content.classList.add('font-large');
                if (btn.dataset.size === 'xlarge') content.classList.add('font-xlarge');
            });
        });
    },

    // ── SHARE ──
    shareFacebook() {
        window.open(`https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(window.location.href)}`, '_blank', 'width=600,height=400');
    },

    copyLink() {
        navigator.clipboard.writeText(window.location.href).then(() => {
            const btn = document.getElementById('btn-copy');
            if (btn) { btn.textContent = '✓ Đã sao chép'; setTimeout(() => btn.textContent = 'Sao chép link', 2000); }
        });
    },

    printArticle() {
        window.print();
    },

    // ── IMAGE PREVIEW ──
    initImagePreview() {
        const input = document.getElementById('article-image');
        const preview = document.getElementById('img-preview');
        if (!input || !preview) return;

        input.addEventListener('change', (e) => {
            const file = e.target.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = (ev) => { preview.src = ev.target.result; preview.style.display = 'block'; };
                reader.readAsDataURL(file);
            }
        });
    },

    // ── DELETE ARTICLE ──
    async deleteArticle(id) {
        if (!confirm('Bạn có chắc muốn xóa bài viết này?')) return;
        try {
            const res = await fetch(`/Article/DeleteArticle/${id}`, { method: 'POST' });
            if (res.ok) { location.reload(); }
            else { alert('Không thể xóa bài viết'); }
        } catch (e) { alert('Có lỗi xảy ra'); }
    },

    // ── SAFEWIKI FLIPBOOK ──
    wikiCurrentPage: 0,
    wikiTotalPages: 0,

    initSafeWiki() {
        const pages = document.querySelectorAll('.wiki-page');
        this.wikiTotalPages = pages.length;
        if (this.wikiTotalPages === 0) return;
        this.showWikiPage(0);
    },

    showWikiPage(index) {
        const pages = document.querySelectorAll('.wiki-page');
        pages.forEach(p => p.classList.remove('active'));
        pages[index].classList.add('active');
        this.wikiCurrentPage = index;

        const prevBtn = document.getElementById('wiki-prev');
        const nextBtn = document.getElementById('wiki-next');
        const progress = document.getElementById('wiki-progress');

        if (prevBtn) prevBtn.disabled = index === 0;
        if (nextBtn) nextBtn.disabled = index === this.wikiTotalPages - 1;
        if (progress) progress.textContent = `Trang ${index + 1} / ${this.wikiTotalPages}`;
    },

    wikiPrev() { if (this.wikiCurrentPage > 0) this.showWikiPage(this.wikiCurrentPage - 1); },
    wikiNext() { if (this.wikiCurrentPage < this.wikiTotalPages - 1) this.showWikiPage(this.wikiCurrentPage + 1); },

    // ── ADMIN ──
    async approveArticle(id) {
        if (!confirm('Duyệt bài viết này?')) return;
        try {
            const res = await fetch(`/Article/Approve/${id}`, { method: 'POST' });
            if (res.ok) { location.reload(); }
            else { alert('Có lỗi xảy ra'); }
        } catch (e) { alert('Có lỗi xảy ra'); }
    },

    rejectArticleId: null,
    openRejectModal(id) {
        this.rejectArticleId = id;
        document.getElementById('reject-modal')?.classList.add('show');
    },
    closeRejectModal() {
        document.getElementById('reject-modal')?.classList.remove('show');
        this.rejectArticleId = null;
    },

    async submitReject() {
        const reason = document.getElementById('reject-reason')?.value?.trim();
        if (!reason) { alert('Vui lòng nhập lý do từ chối'); return; }
        try {
            const res = await fetch(`/Article/Reject/${this.rejectArticleId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ reason })
            });
            if (res.ok) { location.reload(); }
            else { alert('Có lỗi xảy ra'); }
        } catch (e) { alert('Có lỗi xảy ra'); }
    },

    // ── INIT ──
    init() {
        this.initNotifications();
        this.initComments();
        this.initFontControls();
        this.initImagePreview();
        this.initSafeWiki();
    }
};

document.addEventListener('DOMContentLoaded', () => {
    ArticleApp.init();

    // Init draggable widgets
    document.querySelectorAll('.cr-widget').forEach(w => {
        w.addEventListener('dragstart', e => {
            e.dataTransfer.setData('text/plain', w.dataset.type);
            e.dataTransfer.effectAllowed = 'copy';
            w.style.opacity = '0.5';
        });
        w.addEventListener('dragend', () => w.style.opacity = '1');
    });

    // Init builder image preview
    const builderImg = document.getElementById('builderImg');
    if (builderImg) {
        builderImg.addEventListener('change', function() {
            if (!this.files[0]) return;
            const reader = new FileReader();
            reader.onload = e => {
                const prev = document.getElementById('builderImgPreview');
                const lbl  = document.getElementById('builderImgLabel');
                if (prev) { prev.src = e.target.result; prev.style.display = 'block'; }
                if (lbl)  lbl.textContent = '✓ ' + this.files[0].name;
            };
            reader.readAsDataURL(this.files[0]);
        });
    }
});

// ════════════════════════════════════════════════════════════
// UNIFIED BUILDER JS
// ════════════════════════════════════════════════════════════

function crTab(tab) {
    ['widgets','settings'].forEach(t => {
        document.getElementById('tab-' + t)?.classList.toggle('cr-tab--active', t === tab);
        document.getElementById('panel-' + t).style.display = t === tab ? 'block' : 'none';
    });
}

function crOnInput(el, emptyId) {
    const hint = document.getElementById(emptyId);
    if (hint) hint.classList.toggle('hidden', el.innerText.trim().length > 0);
}

function crAppend(editorId, html) {
    const ed = document.getElementById(editorId);
    if (!ed) return;
    ed.focus();
    const sel = window.getSelection();
    if (sel.rangeCount > 0) {
        const range = sel.getRangeAt(0);
        if (ed.contains(range.commonAncestorContainer)) {
            document.execCommand('insertHTML', false, html);
            return;
        }
    }
    const r = document.createRange();
    r.selectNodeContents(ed);
    r.collapse(false);
    sel.removeAllRanges();
    sel.addRange(r);
    document.execCommand('insertHTML', false, html);
}

function crBlockHtml(type) {
    switch (type) {
        case 'heading':  return '<h2>Tiêu đề phần</h2>';
        case 'text':     return '<p>Nhập nội dung đoạn văn tại đây...</p>';
        case 'divider':  return '<hr>';
        case 'quote':    return '<blockquote>Nhập nội dung trích dẫn tại đây...</blockquote>';
        case 'list':     return '<ul><li>Mục 1</li><li>Mục 2</li><li>Mục 3</li></ul>';
        default:         return '<p>Nội dung</p>';
    }
}

function crInsert(type) {
    const ed = document.getElementById('builderEditor');
    const hint = document.getElementById('builderEmpty');
    if (!ed) return;
    if (hint) hint.classList.add('hidden');

    if (type === 'image') {
        const inp = document.getElementById('builderImg');
        const prev = document.getElementById('builderImgPreview');
        if (prev && prev.src && prev.style.display !== 'none') {
            crAppend('builderEditor', `<figure><img src="${prev.src}" alt="Ảnh"/><figcaption>Chú thích ảnh</figcaption></figure>`);
        } else if (inp) {
            inp.click();
        }
        return;
    }
    crAppend('builderEditor', crBlockHtml(type));
}

function crDrop(e) {
    e.preventDefault();
    const canvas = e.currentTarget;
    if (canvas) canvas.classList.remove('cr-dragover');
    const type = e.dataTransfer.getData('text/plain');
    if (!type) return;
    crInsert(type);
}

function crSubmit() {
    const title   = document.getElementById('builderTitle');
    const editor  = document.getElementById('builderEditor');
    const hidden  = document.getElementById('builderContentHidden');
    const alertEl = document.getElementById('builderAlert');

    const warn = msg => { if (alertEl) { alertEl.textContent = msg; alertEl.style.display = 'block'; } };

    if (!title?.value.trim()) {
        crTab('settings');
        warn('⚠️ Vui lòng nhập tiêu đề bài viết.');
        title?.focus(); return;
    }
    const content = editor?.innerHTML.trim();
    if (!content) { warn('⚠️ Vui lòng nhập nội dung bài viết.'); editor?.focus(); return; }
    if (hidden) hidden.value = content;
    if (alertEl) alertEl.style.display = 'none';
    document.getElementById('builderForm').submit();
}
