// ═══════════════════════════════════════════════════════════
// MAP-REPORT.JS — Form báo cáo sự cố (sidebar)
// ═══════════════════════════════════════════════════════════

const ReportForm = {
    isOpen: false,
    isPinMode: false,
    pinMarker: null,
    selectedFiles: [],

    init(map) {
        this.map = map;
        this.sidebar = document.getElementById('sidebar-report');
        this.form = document.getElementById('reportForm');
        this.btnReport = document.getElementById('btn-report');
        this.btnClose = document.getElementById('btn-close-report');
        this.pinBanner = document.getElementById('pin-mode-banner');
        this.submitBtn = document.getElementById('submitBtn');
        this.uploadZone = document.getElementById('uploadZone');
        this.fileInput = document.getElementById('mediaFiles');
        this.previewContainer = document.getElementById('previewContainer');

        this._bindEvents();
        this._loadAlertTypes();
        this._setDefaultTime();
    },

    _bindEvents() {
        // Mở/đóng sidebar
        this.btnReport.addEventListener('click', () => this.toggle());
        this.btnClose.addEventListener('click', () => this.close());
        document.getElementById('cancelPin').addEventListener('click', () => this.cancelPin());

        // Form events
        this.form.addEventListener('submit', (e) => this._handleSubmit(e));

        // Char count
        const desc = document.getElementById('description');
        const counter = document.getElementById('charCount');
        desc.addEventListener('input', () => {
            const len = desc.value.length;
            counter.textContent = len;
            counter.parentElement.classList.toggle('valid', len >= 20);
        });

        // Checkbox → enable submit
        document.getElementById('userConfirmed').addEventListener('change', (e) => {
            this.submitBtn.disabled = !e.target.checked;
        });

        // Upload zone
        this.uploadZone.addEventListener('click', () => this.fileInput.click());
        this.fileInput.addEventListener('change', (e) => this._handleFiles(e.target.files));

        // Drag & drop
        this.uploadZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            this.uploadZone.style.borderColor = 'var(--map-primary)';
        });
        this.uploadZone.addEventListener('dragleave', () => {
            this.uploadZone.style.borderColor = '';
        });
        this.uploadZone.addEventListener('drop', (e) => {
            e.preventDefault();
            this.uploadZone.style.borderColor = '';
            this._handleFiles(e.dataTransfer.files);
        });
    },

    toggle() {
        if (this.isOpen) {
            this.close();
        } else {
            this.open();
        }
    },

    open() {
        // Kiểm tra đăng nhập
        const token = localStorage.getItem('token');
        if (!token) {
            showToast('Vui lòng đăng nhập để báo cáo sự cố', 'error');
            window.location.href = '/Auth/Login';
            return;
        }

        this.isOpen = true;
        this.sidebar.classList.add('open');
        this.btnReport.classList.add('active');
        this.startPinMode();
    },

    close() {
        this.isOpen = false;
        this.sidebar.classList.remove('open');
        this.btnReport.classList.remove('active');
        this.cancelPin();
    },

    // ── Chế độ ghim vị trí ──
    startPinMode() {
        this.isPinMode = true;
        this.pinBanner.style.display = 'block';
        this.map.getContainer().style.cursor = 'crosshair';

        this._pinClickHandler = (e) => {
            this.setPin(e.latlng.lat, e.latlng.lng);
        };
        this.map.on('click', this._pinClickHandler);
    },

    setPin(lat, lng) {
        // Xóa pin cũ
        if (this.pinMarker) {
            this.map.removeLayer(this.pinMarker);
        }

        // Tạo pin mới
        this.pinMarker = L.marker([lat, lng], {
            icon: L.divIcon({
                className: 'pin-marker',
                html: `<div class="marker-icon" style="background:var(--map-warning)"><span>📍</span></div>`,
                iconSize: [36, 36],
                iconAnchor: [18, 36]
            }),
            draggable: true
        }).addTo(this.map);

        // Cho phép kéo pin
        this.pinMarker.on('dragend', (e) => {
            const pos = e.target.getLatLng();
            document.getElementById('lat').value = pos.lat.toFixed(8);
            document.getElementById('lng').value = pos.lng.toFixed(8);
        });

        // Cập nhật form
        document.getElementById('lat').value = lat.toFixed(8);
        document.getElementById('lng').value = lng.toFixed(8);

        // Cập nhật UI
        const pinStatus = document.getElementById('pinStatus');
        pinStatus.className = 'pin-status pin-status--set';
        pinStatus.querySelector('span').textContent = `${lat.toFixed(5)}, ${lng.toFixed(5)}`;

        // Tắt chế độ ghim
        this.isPinMode = false;
        this.pinBanner.style.display = 'none';
        this.map.getContainer().style.cursor = '';
        this.map.off('click', this._pinClickHandler);
    },

    cancelPin() {
        this.isPinMode = false;
        this.pinBanner.style.display = 'none';
        this.map.getContainer().style.cursor = '';
        if (this._pinClickHandler) {
            this.map.off('click', this._pinClickHandler);
        }
    },

    // ── Load danh sách loại sự cố ──
    async _loadAlertTypes() {
        try {
            const types = await MapAPI.getAlertTypes();
            const select = document.getElementById('alertType');
            let currentCategory = '';

            types.forEach(t => {
                if (t.categoryName !== currentCategory) {
                    const optgroup = document.createElement('optgroup');
                    optgroup.label = t.categoryName;
                    select.appendChild(optgroup);
                    currentCategory = t.categoryName;
                }
                const option = document.createElement('option');
                option.value = t.id;
                option.textContent = `${t.iconEmoji || ''} ${t.name}`;
                select.lastElementChild.appendChild(option);
            });
        } catch (e) {
            console.error('Không thể tải danh sách loại sự cố:', e);
        }
    },

    _setDefaultTime() {
        const now = new Date();
        now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
        document.getElementById('incidentTime').value = now.toISOString().slice(0, 16);
    },

    // ── Xử lý upload file ──
    _handleFiles(files) {
        Array.from(files).forEach(file => {
            if (file.size > 10 * 1024 * 1024) {
                showToast('File quá lớn (tối đa 10MB)', 'error');
                return;
            }
            this.selectedFiles.push(file);
            this._renderPreview(file);
        });
    },

    _renderPreview(file) {
        const div = document.createElement('div');
        div.className = 'preview-item';

        if (file.type.startsWith('image/')) {
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            div.appendChild(img);
        } else {
            div.innerHTML = `<div style="display:flex;align-items:center;justify-content:center;height:100%;background:var(--map-surface);color:var(--map-text-muted);font-size:11px;">🎬 Video</div>`;
        }

        const removeBtn = document.createElement('button');
        removeBtn.className = 'remove-preview';
        removeBtn.innerHTML = '×';
        removeBtn.onclick = () => {
            const idx = this.selectedFiles.indexOf(file);
            if (idx > -1) this.selectedFiles.splice(idx, 1);
            div.remove();
        };
        div.appendChild(removeBtn);

        this.previewContainer.appendChild(div);
    },

    // ── Gửi form ──
    async _handleSubmit(e) {
        e.preventDefault();

        const lat = document.getElementById('lat').value;
        const lng = document.getElementById('lng').value;
        if (!lat || !lng) {
            showToast('Vui lòng click vào bản đồ để ghim vị trí', 'error');
            this.startPinMode();
            return;
        }

        const desc = document.getElementById('description').value;
        if (desc.length < 20) {
            showToast('Mô tả phải có ít nhất 20 ký tự', 'error');
            return;
        }

        // Tạo FormData
        const formData = new FormData();
        formData.append('AlertTypeId', document.getElementById('alertType').value);
        formData.append('Latitude', lat);
        formData.append('Longitude', lng);
        formData.append('AddressText', document.getElementById('addressText').value);
        formData.append('Title', document.getElementById('title').value);
        formData.append('Description', desc);
        formData.append('IncidentTime', document.getElementById('incidentTime').value);
        formData.append('UserConfirmed', document.getElementById('userConfirmed').checked);

        // Files
        this.selectedFiles.forEach(f => formData.append('mediaFiles', f));

        // Loading state
        this.submitBtn.classList.add('loading');
        this.submitBtn.textContent = 'Đang gửi...';

        try {
            const result = await MapAPI.submitAlert(formData);
            if (result.success) {
                showToast('✅ Báo cáo đã được ghi nhận!', 'success');
                this._resetForm();
                this.close();
                // Reload markers
                if (typeof MapCore !== 'undefined') MapCore.loadAlerts();
            } else {
                showToast(result.message || 'Có lỗi xảy ra', 'error');
            }
        } catch (err) {
            showToast('Lỗi kết nối. Vui lòng thử lại.', 'error');
            console.error(err);
        } finally {
            this.submitBtn.classList.remove('loading');
            this.submitBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="18" height="18"><line x1="22" y1="2" x2="11" y2="13"></line><polygon points="22 2 15 22 11 13 2 9 22 2"></polygon></svg> Gửi báo cáo`;
        }
    },

    _resetForm() {
        this.form.reset();
        this.selectedFiles = [];
        this.previewContainer.innerHTML = '';
        document.getElementById('lat').value = '';
        document.getElementById('lng').value = '';
        document.getElementById('pinStatus').className = 'pin-status pin-status--empty';
        document.getElementById('pinStatus').querySelector('span').textContent = 'Click vào bản đồ để ghim vị trí';
        document.getElementById('charCount').textContent = '0';
        this.submitBtn.disabled = true;
        this._setDefaultTime();
        if (this.pinMarker) {
            this.map.removeLayer(this.pinMarker);
            this.pinMarker = null;
        }
    }
};

// ═══ TOAST HELPER ═══
function showToast(message, type = 'success') {
    const existing = document.querySelector('.map-toast');
    if (existing) existing.remove();

    const toast = document.createElement('div');
    toast.className = `map-toast ${type}`;
    toast.textContent = message;
    document.body.appendChild(toast);

    setTimeout(() => toast.remove(), 3500);
}
