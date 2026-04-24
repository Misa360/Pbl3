// ═══════════════════════════════════════════════════════════
// MAP-DATA.JS — Gọi API lấy/gửi dữ liệu
// ═══════════════════════════════════════════════════════════

const MapAPI = {
    baseUrl: '/api/alerts',

    // Header có JWT token
    _authHeaders() {
        const token = localStorage.getItem('token');
        const headers = {};
        if (token) headers['Authorization'] = `Bearer ${token}`;
        return headers;
    },

    // ── Lấy danh sách loại sự cố ──
    async getAlertTypes() {
        const res = await fetch(`${this.baseUrl}/types`);
        return await res.json();
    },

    // ── Lấy markers cho bản đồ ──
    async getMapAlerts(bounds, fromTime, toTime) {
        const params = new URLSearchParams({
            southLat: bounds.getSouth(),
            northLat: bounds.getNorth(),
            westLng: bounds.getWest(),
            eastLng: bounds.getEast(),
            fromTime: fromTime.toISOString(),
            toTime: toTime.toISOString()
        });
        const res = await fetch(`${this.baseUrl}/map?${params}`);
        return await res.json();
    },

    // ── Lấy dữ liệu heatmap ──
    async getHeatmapData(fromTime, toTime) {
        const params = new URLSearchParams({
            fromTime: fromTime.toISOString(),
            toTime: toTime.toISOString()
        });
        const res = await fetch(`${this.baseUrl}/heatmap?${params}`);
        return await res.json();
    },

    // ── Chi tiết 1 alert ──
    async getAlertDetail(id) {
        const res = await fetch(`${this.baseUrl}/${id}`);
        return await res.json();
    },

    // ── Tạo báo cáo mới ──
    async submitAlert(formData) {
        const res = await fetch(this.baseUrl, {
            method: 'POST',
            headers: this._authHeaders(),
            body: formData
        });
        return await res.json();
    },

    // ── Xác nhận / Phản bác ──
    async verifyAlert(id, data) {
        const res = await fetch(`${this.baseUrl}/${id}/verify`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...this._authHeaders()
            },
            body: JSON.stringify(data)
        });
        return await res.json();
    }
};
