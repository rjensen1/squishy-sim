// PROTOTYPE: SquishySim web client
'use strict';

const DRIVES = ['hunger', 'thirst', 'fatigue', 'bladder', 'mood'];
const DEFAULT_DRIVES = { hunger: 0.10, thirst: 0.10, fatigue: 0.10, bladder: 0.00, mood: 0.70 };

let selectedAgentId = null;
let showingConversations = false;

// ── Bootstrap ────────────────────────────────────────────────────────────────

window.addEventListener('DOMContentLoaded', () => {
    bindControls();
    refresh();
    setInterval(refresh, 1500);
});

function bindControls() {
    document.getElementById('btn-step').addEventListener('click', () => api('POST', '/sim/step').then(refresh));
    document.getElementById('btn-pause').addEventListener('click', () => api('POST', '/sim/pause').then(refresh));
    document.getElementById('btn-resume').addEventListener('click', () => api('POST', '/sim/resume').then(refresh));
    document.getElementById('speed-select').addEventListener('change', e =>
        api('POST', '/sim/speed', { multiplier: parseFloat(e.target.value) }));

    document.getElementById('btn-conversations').addEventListener('click', () => {
        showingConversations = !showingConversations;
        document.getElementById('btn-conversations').classList.toggle('active', showingConversations);
        document.getElementById('agent-detail').classList.toggle('hidden', showingConversations);
        document.getElementById('conversations-panel').classList.toggle('hidden', !showingConversations);
        if (showingConversations) refreshConversations();
    });

    document.getElementById('btn-reset-drives').addEventListener('click', resetDrives);
    document.getElementById('btn-save-llm').addEventListener('click', saveLlm);
}

// ── Main refresh cycle ───────────────────────────────────────────────────────

async function refresh() {
    const [agentsData, statusData] = await Promise.all([
        api('GET', '/agents'),
        api('GET', '/sim/status'),
    ]);

    document.getElementById('tick-display').textContent = `Tick ${statusData.tick}`;

    const isPaused = statusData.paused;
    document.getElementById('btn-pause').classList.toggle('active', isPaused === true);
    document.getElementById('btn-resume').classList.toggle('active', isPaused === false);

    renderAgentList(agentsData);
    if (selectedAgentId) refreshAgentDetail();
    if (showingConversations) refreshConversations();
}

// ── Agent list ───────────────────────────────────────────────────────────────

function renderAgentList(agents) {
    const ul = document.getElementById('agent-list');
    // Preserve selection; only rebuild if agent set changed
    const ids = agents.map(a => a.id).join(',');
    if (ul.dataset.ids === ids) return;
    ul.dataset.ids = ids;
    ul.innerHTML = '';
    agents.forEach(a => {
        const li = document.createElement('li');
        li.textContent = a.name;
        li.dataset.id = a.id;
        if (a.id === selectedAgentId) li.classList.add('selected');
        li.addEventListener('click', () => selectAgent(a.id));
        ul.appendChild(li);
    });
    if (!selectedAgentId && agents.length > 0) selectAgent(agents[0].id);
}

function selectAgent(id) {
    selectedAgentId = id;
    showingConversations = false;
    document.getElementById('btn-conversations').classList.remove('active');
    document.getElementById('agent-detail').classList.remove('hidden');
    document.getElementById('conversations-panel').classList.add('hidden');
    document.querySelectorAll('#agent-list li').forEach(li =>
        li.classList.toggle('selected', li.dataset.id === id));
    refreshAgentDetail();
}

// ── Agent detail ─────────────────────────────────────────────────────────────

async function refreshAgentDetail() {
    if (!selectedAgentId) return;
    const agent = await api('GET', `/agents/${selectedAgentId}`);
    document.getElementById('agent-name').textContent = agent.name;
    renderDrives(agent.drives);
    document.getElementById('current-action').textContent = agent.currentAction;
    document.getElementById('current-reason').textContent = agent.currentReason;
    document.getElementById('llm-model').value = agent.llmConfig.model;
    document.getElementById('llm-url').value   = agent.llmConfig.baseUrl;
    await refreshThoughts();
    await refreshAgentMessages();
}

function renderDrives(drives) {
    const grid = document.getElementById('drives-grid');
    DRIVES.forEach(d => {
        let row = document.getElementById(`drive-row-${d}`);
        if (!row) {
            row = document.createElement('div');
            row.className = 'drive-row';
            row.id = `drive-row-${d}`;
            row.innerHTML = `
                <span class="drive-label">${d}</span>
                <div class="drive-bar-bg"><div class="drive-bar-fill" id="bar-${d}"></div></div>
                <input class="drive-slider" id="slider-${d}" type="range" min="0" max="100" step="1">
                <span class="drive-num" id="num-${d}"></span>`;
            grid.appendChild(row);

            document.getElementById(`slider-${d}`).addEventListener('input', e => {
                const val = parseInt(e.target.value) / 100;
                updateDriveUI(d, val);
            });
            document.getElementById(`slider-${d}`).addEventListener('change', e => {
                const val = parseInt(e.target.value) / 100;
                api('POST', `/agents/${selectedAgentId}/drives/${d}`, { value: val });
            });
        }

        const val = drives[d];
        updateDriveUI(d, val);
    });
}

function updateDriveUI(drive, val) {
    const bar = document.getElementById(`bar-${drive}`);
    const slider = document.getElementById(`slider-${drive}`);
    const num = document.getElementById(`num-${drive}`);
    if (!bar) return;
    const pct = Math.round(val * 100);
    bar.style.width = `${pct}%`;
    if (drive === 'mood') {
        bar.className = 'drive-bar-fill' + (val < 0.15 ? ' crit' : val < 0.35 ? ' warn' : '');
    } else {
        bar.className = 'drive-bar-fill' + (val > 0.85 ? ' crit' : val > 0.65 ? ' warn' : '');
    }
    slider.value = pct;
    num.textContent = val.toFixed(2);
}

async function refreshThoughts() {
    const thoughts = await api('GET', `/agents/${selectedAgentId}/thoughts?limit=20`);
    const log = document.getElementById('thought-log');
    log.innerHTML = '';
    thoughts.forEach(t => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerHTML = `<span class="ts">${fmtTime(t.timestamp)}</span><span class="text">${esc(t.text)}</span>`;
        log.appendChild(div);
    });
    log.scrollTop = log.scrollHeight;
}

async function refreshAgentMessages() {
    const msgs = await api('GET', `/agents/${selectedAgentId}/messages`);
    const el = document.getElementById('agent-messages');
    el.innerHTML = '';
    msgs.forEach(m => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerHTML = `<span class="ts">${fmtTime(m.timestamp)}</span><span class="from">${esc(m.fromAgentId)}</span><span class="to">→ ${esc(m.toAgentId)}</span><span class="text">${esc(m.text)}</span>`;
        el.appendChild(div);
    });
    el.scrollTop = el.scrollHeight;
}

async function refreshConversations() {
    const msgs = await api('GET', '/conversations');
    const feed = document.getElementById('global-feed');
    feed.innerHTML = '';
    msgs.forEach(m => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerHTML = `<span class="ts">${fmtTime(m.timestamp)}</span><span class="from">${esc(m.fromAgentId)}</span><span class="to">→ ${esc(m.toAgentId)}</span><span class="text">${esc(m.text)}</span>`;
        feed.appendChild(div);
    });
    feed.scrollTop = feed.scrollHeight;
}

// ── Actions ──────────────────────────────────────────────────────────────────

async function resetDrives() {
    if (!selectedAgentId) return;
    for (const [d, v] of Object.entries(DEFAULT_DRIVES)) {
        await api('POST', `/agents/${selectedAgentId}/drives/${d}`, { value: v });
    }
    refreshAgentDetail();
}

async function saveLlm() {
    if (!selectedAgentId) return;
    const model   = document.getElementById('llm-model').value.trim();
    const baseUrl = document.getElementById('llm-url').value.trim();
    const apiKey  = document.getElementById('llm-key').value || null;
    await api('PUT', `/agents/${selectedAgentId}/llm`, { model, baseUrl, apiKey });
    document.getElementById('llm-key').value = '';
}

// ── Helpers ───────────────────────────────────────────────────────────────────

async function api(method, path, body) {
    const opts = { method, headers: { 'Content-Type': 'application/json' } };
    if (body) opts.body = JSON.stringify(body);
    const res = await fetch(path, opts);
    if (!res.ok) { console.error(`${method} ${path} → ${res.status}`); return {}; }
    return res.json();
}

function esc(s) {
    return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

function fmtTime(ts) {
    return new Date(ts).toLocaleTimeString('en-US', { hour12: false,
        hour: '2-digit', minute: '2-digit', second: '2-digit' });
}
