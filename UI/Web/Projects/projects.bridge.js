const zavodProjectsBridge = (() => {
  const bridge = window.chrome?.webview ?? null;

  // ── Element refs ──────────────────────────────────────────────
  const screens = {
    list: document.getElementById('screen-list'),
    home: document.getElementById('screen-home'),
    'work-cycle': document.getElementById('screen-wc')
  };
  const projGrid = document.getElementById('proj-grid');
  const projEmpty = document.getElementById('proj-empty');
  const homeCrumbName = document.getElementById('home-crumb-name');
  const homeProjName = document.getElementById('home-proj-name');
  const homeProjDesc = document.getElementById('home-proj-desc');
  const hsFiles = document.getElementById('hs-files');
  const hsAnchors = document.getElementById('hs-anchors');
  const hsTasks = document.getElementById('hs-tasks');
  const hsDocs = document.getElementById('hs-docs');
  const reportModal = document.getElementById('report-modal');
  const modalProjName = document.getElementById('modal-proj-name');
  const reportCard = document.getElementById('report-card');
  const reportPreview = document.getElementById('report-preview');
  const reportIframe = document.getElementById('report-iframe');
  const homeAnchorsSection = document.getElementById('home-anchors-section');
  const homeAnchorsEl = document.getElementById('home-anchors');
  const homeDocsSection = document.getElementById('home-docs-section');
  const homeDocsEl = document.getElementById('home-docs');
  const newProjectModal = document.getElementById('new-project-modal');
  const npNameInput = document.getElementById('np-name');
  const npKindSelect = document.getElementById('np-kind');
  const npPathLabel = document.getElementById('np-path');
  const npCancelBtn = document.getElementById('np-cancel');
  const npCreateBtn = document.getElementById('np-create');
  const cinput = document.getElementById('cinput');
  const cSendBtn = document.getElementById('c-send');
  const cPlusBtn = document.getElementById('c-plus');
  const composerAttachmentsEl = document.getElementById('composer-attachments');
  const intentBtn = document.getElementById('intent-btn');
  const phaseTag = document.getElementById('phase-tag');
  const composerWrap = document.getElementById('composer-wrap');
  const pfCard = document.getElementById('pf-card');
  const pfRowEls = pfCard ? pfCard.querySelectorAll('.pf-row') : [];
  const pfClarify = document.getElementById('pf-clarify');
  const pfClarifyInput = document.getElementById('pf-clarify-input');
  const phase1 = document.getElementById('phase1');
  const phase3 = document.getElementById('phase3');
  const phase1Overlay = document.getElementById('phase1-overlay');
  const feed = document.getElementById('feed');
  const agentFeed = document.getElementById('agent-feed');
  const artifactBody = document.getElementById('artifact-body');
  const artifactTitle = document.getElementById('artifact-title');
  const leftTimer = document.getElementById('left-timer');
  const leftPulse = document.getElementById('left-pulse');
  const actionBar = document.getElementById('action-bar');
  const abReviseWrap = document.getElementById('ab-revise-wrap');
  const abReviseInput = document.getElementById('ab-revise-input');

  let renderedState = null;
  let localizedText = {};
  let currentProj = null;

  // ── Bridge helpers ────────────────────────────────────────────
  function emit(message) {
    if (!message || !message.type) {
      return;
    }
    if (bridge) {
      bridge.postMessage(message);
    }
  }

  function normalizeIncomingMessage(message) {
    if (!message) {
      return null;
    }
    if (typeof message === 'string') {
      try {
        return JSON.parse(message);
      } catch {
        return null;
      }
    }
    return message;
  }

  // ── Localization ──────────────────────────────────────────────
  function applyLocalizedText(text) {
    localizedText = { ...localizedText, ...(text || {}) };

    document.querySelectorAll('[data-l10n]').forEach((el) => {
      const key = el.dataset.l10n;
      if (key && localizedText[key] !== undefined) {
        el.textContent = localizedText[key];
      }
    });

    document.querySelectorAll('[data-l10n-placeholder]').forEach((el) => {
      const key = el.dataset.l10nPlaceholder;
      if (key && localizedText[key] !== undefined) {
        el.placeholder = localizedText[key];
      }
    });

    document.querySelectorAll('[data-l10n-title]').forEach((el) => {
      const key = el.dataset.l10nTitle;
      if (key && localizedText[key] !== undefined) {
        el.title = localizedText[key];
        el.setAttribute('aria-label', localizedText[key]);
      }
    });
  }

  // ── Screen switching ──────────────────────────────────────────
  function showScreen(name) {
    const next = screens[name];
    if (!next) {
      return;
    }
    const current = document.querySelector('.screen.active');
    if (current && current !== next) {
      current.classList.remove('visible');
      setTimeout(() => {
        current.classList.remove('active');
        next.classList.add('active');
        requestAnimationFrame(() => requestAnimationFrame(() => next.classList.add('visible')));
      }, 150);
    } else {
      next.classList.add('active');
      requestAnimationFrame(() => requestAnimationFrame(() => next.classList.add('visible')));
    }
  }

  // ── State render ──────────────────────────────────────────────
  function render(state) {
    if (!state) {
      return;
    }
    renderedState = state;

    if (state.text) {
      applyLocalizedText(state.text);
    }

    if (state.currentScreen) {
      showScreen(state.currentScreen);
    }

    if (state.workCycle && state.workCycle.visualPhase) {
      document.body.dataset.phase = state.workCycle.visualPhase;
      if (phaseTag && localizedText[`projects.phase.${state.workCycle.visualPhase}`]) {
        phaseTag.textContent = localizedText[`projects.phase.${state.workCycle.visualPhase}`];
      }
    }

    renderComposerAttachments(state.conversation?.composer?.pendingAttachments);
    renderProjectList(state.list);

    // Project home headline (if state provides selected project)
    if (state.selectedProject) {
      const p = state.selectedProject;
      currentProj = p.id || p.name || null;
      if (homeCrumbName && p.name) homeCrumbName.textContent = p.name;
      if (homeProjName && p.name) homeProjName.textContent = p.name;
      if (homeProjDesc) homeProjDesc.textContent = p.description || '';
      if (hsFiles && Number.isFinite(p.files)) hsFiles.textContent = String(p.files);
      if (hsAnchors && Number.isFinite(p.anchors)) hsAnchors.textContent = String(p.anchors);
      if (hsTasks && Number.isFinite(p.tasks)) hsTasks.textContent = String(p.tasks);
      if (hsDocs && Number.isFinite(p.docs)) hsDocs.textContent = String(p.docs);

      const previewUrl = p.previewUrl || '';
      const previewVisible = !!previewUrl;
      if (reportCard) reportCard.hidden = !previewVisible;
      if (reportPreview && reportPreview.src !== previewUrl) {
        reportPreview.src = previewUrl || 'about:blank';
      }
      if (reportIframe && previewVisible && reportIframe.src !== previewUrl) {
        reportIframe.src = previewUrl;
      }
      if (modalProjName) modalProjName.textContent = p.name || '—';

      renderHomeAnchors(p.anchorRows);
      renderHomeDocuments(p.documentRows);
    } else {
      renderHomeAnchors(null);
      renderHomeDocuments(null);
    }

    requestAnimationFrame(() => {
      emit({ type: 'render_complete', payload: {} });
    });
  }

  // ── New project modal ─────────────────────────────────────────
  function slugifyForFolder(value) {
    if (!value) return '';
    let out = '';
    let prevSep = false;
    for (const ch of value) {
      const code = ch.codePointAt(0);
      const isLetter = /\p{L}/u.test(ch);
      const isDigit = code >= 0x30 && code <= 0x39;
      if (isLetter || isDigit) {
        out += ch.toLowerCase();
        prevSep = false;
      } else if (out.length > 0 && !prevSep) {
        out += '-';
        prevSep = true;
      }
    }
    return out.replace(/^-+|-+$/g, '');
  }

  function updateNewProjectPathPreview() {
    if (!npPathLabel) return;
    const raw = (npNameInput?.value || '').trim();
    const slug = slugifyForFolder(raw) || '—';
    npPathLabel.textContent = `Documents/ZAVOD/${slug}`;
    if (npCreateBtn) npCreateBtn.disabled = raw.length === 0;
  }

  function openNewProjectModal() {
    if (!newProjectModal) return;
    if (npNameInput) npNameInput.value = '';
    if (npKindSelect) npKindSelect.value = 'generic';
    updateNewProjectPathPreview();
    newProjectModal.classList.add('open');
    setTimeout(() => npNameInput?.focus(), 60);
  }

  function closeNewProjectModal() {
    if (!newProjectModal) return;
    newProjectModal.classList.remove('open');
  }

  function submitNewProject() {
    const name = (npNameInput?.value || '').trim();
    if (!name) return;
    const kind = npKindSelect?.value || 'generic';
    emit({ type: 'create_project', payload: { name, kind } });
    closeNewProjectModal();
  }

  if (npNameInput) {
    npNameInput.addEventListener('input', updateNewProjectPathPreview);
    npNameInput.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        submitNewProject();
      } else if (event.key === 'Escape') {
        event.preventDefault();
        closeNewProjectModal();
      }
    });
  }
  if (npCreateBtn) npCreateBtn.addEventListener('click', submitNewProject);
  if (npCancelBtn) npCancelBtn.addEventListener('click', closeNewProjectModal);
  if (newProjectModal) {
    newProjectModal.addEventListener('click', (event) => {
      if (event.target === newProjectModal) closeNewProjectModal();
    });
  }

  // ── Home: scanner-analysis anchor rows ────────────────────────
  function renderHomeAnchors(rows) {
    if (!homeAnchorsEl || !homeAnchorsSection) return;
    const list = Array.isArray(rows) ? rows : [];
    homeAnchorsEl.innerHTML = '';
    homeAnchorsSection.hidden = list.length === 0;

    list.forEach((row) => {
      if (!row) return;
      const el = document.createElement('div');
      el.className = 'anchor-row';
      const tag = document.createElement('span');
      tag.className = 'anchor-tag';
      tag.textContent = row.tag || '';
      const val = document.createElement('span');
      val.className = 'anchor-val';
      val.textContent = row.value || '';
      el.appendChild(tag);
      el.appendChild(val);
      homeAnchorsEl.appendChild(el);
    });
  }

  // ── Home: user document rows ──────────────────────────────────
  function renderHomeDocuments(rows) {
    if (!homeDocsEl || !homeDocsSection) return;
    const list = Array.isArray(rows) ? rows : [];
    homeDocsEl.innerHTML = '';
    homeDocsSection.hidden = list.length === 0;

    list.forEach((row) => {
      if (!row) return;
      const el = document.createElement('div');
      el.className = 'doc-row';
      const left = document.createElement('div');
      left.className = 'doc-row-left';
      const name = document.createElement('div');
      name.className = 'doc-row-name';
      name.textContent = row.name || '';
      left.appendChild(name);
      const meta = document.createElement('div');
      meta.className = 'doc-row-meta';
      meta.textContent = row.meta || '';
      el.appendChild(left);
      el.appendChild(meta);
      homeDocsEl.appendChild(el);
    });
  }

  // ── Project list cards ────────────────────────────────────────
  function renderProjectList(list) {
    if (!projGrid) {
      return;
    }
    if (!list) {
      // No payload from C# — leave grid as-is (browser fallback may want hardcoded cards)
      return;
    }
    const projects = Array.isArray(list.projects) ? list.projects : [];
    projGrid.innerHTML = '';

    projects.forEach((project) => {
      if (!project || !project.projectId) return;
      const card = document.createElement('div');
      card.className = 'proj-card';
      card.dataset.proj = project.projectId;

      const top = document.createElement('div');
      top.className = 'proj-card-top';
      const name = document.createElement('div');
      name.className = 'proj-name';
      name.textContent = project.projectName || project.projectId;
      const status = document.createElement('div');
      const statusKind = (project.status || 'idle').toLowerCase();
      status.className = `proj-status ${statusKind}`;
      const statusText = localizedText[`projects.status.${statusKind}`];
      status.textContent = statusText || statusKind;
      top.appendChild(name);
      top.appendChild(status);
      card.appendChild(top);

      if (project.description) {
        const desc = document.createElement('div');
        desc.className = 'proj-desc';
        desc.textContent = project.description;
        card.appendChild(desc);
      }

      const meta = document.createElement('div');
      meta.className = 'proj-meta';
      const metaLeft = document.createElement('div');
      metaLeft.className = 'proj-meta-left';
      if (project.lastActivity) {
        const activity = document.createElement('div');
        activity.className = 'proj-meta-item';
        const activitySpan = document.createElement('span');
        activitySpan.textContent = project.lastActivity;
        activity.appendChild(activitySpan);
        metaLeft.appendChild(activity);
      }
      const stats = `${project.fileCount ?? 0} · ${project.anchorCount ?? 0}`;
      const statsItem = document.createElement('div');
      statsItem.className = 'proj-meta-item';
      const statsSpan = document.createElement('span');
      statsSpan.textContent = stats;
      statsItem.appendChild(statsSpan);
      metaLeft.appendChild(statsItem);
      meta.appendChild(metaLeft);

      const stack = document.createElement('div');
      stack.className = 'proj-stack';
      (project.stackTags || []).forEach((tag) => {
        const tagEl = document.createElement('span');
        tagEl.className = 'stack-tag';
        tagEl.textContent = tag;
        stack.appendChild(tagEl);
      });
      meta.appendChild(stack);
      card.appendChild(meta);

      projGrid.appendChild(card);
    });

    if (projEmpty) {
      projEmpty.hidden = projects.length > 0;
    }
  }

  // ── Composer attachment chips ─────────────────────────────────
  function renderComposerAttachments(items) {
    if (!composerAttachmentsEl) {
      return;
    }
    const list = Array.isArray(items) ? items : [];
    composerAttachmentsEl.innerHTML = '';
    composerAttachmentsEl.hidden = list.length === 0;

    list.forEach((file) => {
      if (!file || !file.id) return;
      const chip = document.createElement('div');
      chip.className = 'composer-chip';

      const label = document.createElement('span');
      label.className = 'composer-chip-label';
      label.textContent = file.detail
        ? `${file.label} (${file.detail})`
        : file.label;
      chip.appendChild(label);

      const removeBtn = document.createElement('button');
      removeBtn.type = 'button';
      removeBtn.className = 'composer-chip-remove';
      removeBtn.textContent = '\u00d7';
      removeBtn.addEventListener('click', () => {
        emit({ type: 'remove_attachment', payload: { draftId: file.id } });
      });
      chip.appendChild(removeBtn);

      composerAttachmentsEl.appendChild(chip);
    });
  }

  // ── Action wiring (always on; emit to C# when bridged) ────────
  function wireActions() {
    // Project list cards → select_project
    if (projGrid) {
      projGrid.addEventListener('click', (event) => {
        const card = event.target.closest('.proj-card[data-proj]');
        if (!card) return;
        const projectId = card.dataset.proj;
        emit({ type: 'select_project', payload: { projectId } });
        if (!bridge) {
          // Fallback for browser preview
          fallbackOpenHome(projectId);
        }
      });
    }

    document.querySelectorAll('[data-action]').forEach((el) => {
      el.addEventListener('click', (event) => {
        const action = el.dataset.action;
        switch (action) {
          case 'create-project':
            openNewProjectModal();
            break;
          case 'import-project':
            emit({ type: 'import_project', payload: {} });
            break;
          case 'back-to-list':
            emit({ type: 'navigate_screen', payload: { screen: 'list' } });
            if (!bridge) showScreen('list');
            break;
          case 'back-to-home':
            emit({ type: 'navigate_screen', payload: { screen: 'home' } });
            if (!bridge) {
              fallbackBackToPhase1();
              showScreen('home');
            }
            break;
          case 'enter-work':
            emit({ type: 'navigate_screen', payload: { screen: 'work-cycle' } });
            if (!bridge) {
              showScreen('work-cycle');
              setTimeout(() => cinput && cinput.focus(), 80);
            }
            break;
          case 'open-report':
            // UI-only modal toggle, no core action
            fallbackOpenReport();
            break;
          case 'close-report':
            event.stopPropagation();
            // UI-only modal toggle, no core action
            fallbackCloseReport();
            break;
          case 'artifact-refresh':
            // Out of scope for Pass 1 — local fallback only
            break;
          case 'artifact-share':
            // Out of scope for Pass 1 — local fallback only
            break;
          default:
            break;
        }
      });
    });

    // Composer
    if (cSendBtn) cSendBtn.addEventListener('click', submitComposer);
    if (cinput) {
      cinput.addEventListener('input', syncIntent);
      cinput.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' && !event.shiftKey) {
          event.preventDefault();
          submitComposer();
        }
        if (event.key === 'Tab') {
          event.preventDefault();
          emit({ type: 'enter_work', payload: {} });
          if (!bridge) fallbackOpenPreflight();
        }
      });
      cinput.addEventListener('paste', (event) => {
        const pastedText = event.clipboardData?.getData('text/plain') || '';
        const lineCount = pastedText.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n').length;
        if (pastedText.length <= 4000 && lineCount <= 40) {
          return;
        }
        event.preventDefault();
        emit({ type: 'stage_text_artifact', payload: { text: pastedText } });
      });
    }
    if (intentBtn) {
      intentBtn.addEventListener('click', () => {
        emit({ type: 'enter_work', payload: {} });
        if (!bridge) fallbackOpenPreflight();
      });
    }
    if (cPlusBtn) {
      cPlusBtn.addEventListener('click', () => {
        emit({ type: 'request_attach_files', payload: { sourceType: 'file' } });
      });
    }

    // Preflight buttons
    const btnPfConfirm = document.getElementById('btn-pf-confirm');
    const btnPfBack = document.getElementById('btn-pf-back');
    const btnPfClarify = document.getElementById('btn-pf-clarify');
    const pfClarifySend = document.getElementById('pf-clarify-send');

    if (btnPfConfirm) btnPfConfirm.addEventListener('click', () => {
      emit({ type: 'confirm_preflight', payload: {} });
    });
    if (btnPfBack) btnPfBack.addEventListener('click', () => {
      emit({ type: 'return_to_chat', payload: {} });
      if (!bridge) fallbackClosePreflight();
    });
    if (btnPfClarify) btnPfClarify.addEventListener('click', () => {
      // UI-only toggle for clarification input. Submission is the core action.
      fallbackTogglePfClarify();
    });
    if (pfClarifySend) pfClarifySend.addEventListener('click', () => {
      const text = pfClarifyInput?.value.trim() ?? '';
      if (!text) return;
      emit({ type: 'apply_clarification', payload: { text } });
      if (pfClarifyInput) pfClarifyInput.value = '';
      if (!bridge && pfClarify) pfClarify.classList.remove('open');
    });
    if (pfClarifyInput) {
      pfClarifyInput.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' && !event.shiftKey) {
          event.preventDefault();
          pfClarifySend?.click();
        }
        if (event.key === 'Escape' && pfClarify) {
          pfClarify.classList.remove('open');
        }
      });
    }

    // Action bar buttons
    const btnConfirm = document.getElementById('btn-confirm');
    const btnRevise = document.getElementById('btn-revise');
    const btnReject = document.getElementById('btn-reject');
    const abReviseSend = document.getElementById('ab-revise-send');

    if (btnConfirm) btnConfirm.addEventListener('click', () => {
      emit({ type: 'accept_result', payload: {} });
      if (!bridge) fallbackBackToPhase1();
    });
    if (btnRevise) btnRevise.addEventListener('click', () => {
      // UI-only toggle for revise input. Submission is the core action (request_revision).
      fallbackToggleRevise();
    });
    if (btnReject) btnReject.addEventListener('click', () => {
      emit({ type: 'reject_result', payload: {} });
      if (!bridge) fallbackRejectGuard();
    });
    if (abReviseSend) abReviseSend.addEventListener('click', () => {
      const text = abReviseInput?.value.trim() ?? '';
      if (!text) return;
      emit({ type: 'request_revision', payload: { text } });
      if (abReviseInput) abReviseInput.value = '';
      if (!bridge) {
        abReviseWrap?.classList.remove('open');
        fallbackBackToPhase1();
      }
    });
    if (abReviseInput) {
      abReviseInput.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' && !event.shiftKey) {
          event.preventDefault();
          abReviseSend?.click();
        }
        if (event.key === 'Escape' && abReviseWrap) {
          abReviseWrap.classList.remove('open');
        }
      });
    }

    // Modal background click closes report
    if (reportModal) {
      reportModal.addEventListener('click', (event) => {
        if (event.target === reportModal) {
          // UI-only modal toggle, no core action
          fallbackCloseReport();
        }
      });
    }
  }

  function submitComposer() {
    if (!cinput) return;
    const text = cinput.value.trim();
    if (!text) return;
    emit({ type: 'send_message', payload: { text } });
    cinput.value = '';
    syncIntent();
    if (!bridge) fallbackLocalEcho(text);
  }

  function getLevel(v) {
    const w = v.trim().split(/\s+/).filter(Boolean).length;
    const l = v.trim().length;
    if (w >= 6 || l >= 32) return 3;
    if (w >= 3 || l >= 14) return 2;
    if (w >= 1 || l >= 3) return 1;
    return 0;
  }

  function syncIntent() {
    if (!cinput || !intentBtn) return;
    const lvl = getLevel(cinput.value);
    intentBtn.className = 'intent-btn' + (lvl > 0 ? ' lvl' + lvl : '');
  }

  // ── Fallback demo logic (only when no C# bridge present) ──────
  // Preserves the prototype interactions so the HTML can still be
  // opened directly in a browser for design iteration.
  const PROJ_DATA = {
    'myapp-desktop': { name: 'MyApp.Desktop', desc: 'Десктопное приложение на WinUI 3. Нестабильный старт — невалидный конфиг, молчащие ошибки в bootstrap.', files: 31, anchors: 6, tasks: 6, docs: 5 },
    'backend-api': { name: 'backend-api', desc: 'REST API сервис. Миграция на новую схему БД, рефакторинг слоя авторизации.', files: 84, anchors: 12, tasks: 9, docs: 3 },
    'dashboard-web': { name: 'dashboard-web', desc: 'Веб-дашборд для мониторинга. Компоненты на React, интеграция с backend-api.', files: 22, anchors: 8, tasks: 4, docs: 2 }
  };

  function fallbackOpenHome(projId) {
    currentProj = projId;
    const d = PROJ_DATA[projId];
    if (!d) { showScreen('home'); return; }
    if (homeCrumbName) homeCrumbName.textContent = d.name;
    if (homeProjName) homeProjName.textContent = d.name;
    if (homeProjDesc) homeProjDesc.textContent = d.desc;
    if (hsFiles) hsFiles.textContent = d.files;
    if (hsAnchors) hsAnchors.textContent = d.anchors;
    if (hsTasks) hsTasks.textContent = d.tasks;
    if (hsDocs) hsDocs.textContent = d.docs;
    showScreen('home');
  }

  function fallbackOpenReport() {
    if (modalProjName) modalProjName.textContent = currentProj || '—';
    reportModal?.classList.add('open');
  }
  function fallbackCloseReport() { reportModal?.classList.remove('open'); }

  let pfOpen = false;
  function fallbackOpenPreflight() {
    if (pfOpen || !pfCard) return;
    pfOpen = true;
    if (phaseTag) phaseTag.textContent = 'phase-2';
    phase1Overlay?.classList.add('active');
    composerWrap?.classList.add('hidden');
    pfCard.classList.add('open');
    pfRowEls.forEach((r, i) => setTimeout(() => r.classList.add('in'), 260 + i * 60));
  }
  function fallbackClosePreflight() {
    if (!pfOpen || !pfCard) return;
    pfOpen = false;
    if (phaseTag) phaseTag.textContent = 'phase-1';
    pfRowEls.forEach((r) => r.classList.remove('in'));
    pfCard.classList.remove('open');
    setTimeout(() => {
      phase1Overlay?.classList.remove('active');
      composerWrap?.classList.remove('hidden');
      cinput?.focus();
    }, 300);
  }

  let pfClarifyOpen = false;
  function fallbackTogglePfClarify() {
    if (!pfClarify) return;
    pfClarifyOpen = !pfClarifyOpen;
    pfClarify.classList.toggle('open', pfClarifyOpen);
    if (pfClarifyOpen) setTimeout(() => pfClarifyInput?.focus(), 340);
  }

  // Generic DOM helper used by browser-fallback echo (kept for non-bridged preview).
  function mk(cls, html) {
    const e = document.createElement('div');
    e.className = cls;
    if (html) e.innerHTML = html;
    return e;
  }

  let abReviseOpen = false;
  function fallbackToggleRevise() {
    if (!abReviseWrap) return;
    abReviseOpen = !abReviseOpen;
    abReviseWrap.classList.toggle('open', abReviseOpen);
    if (abReviseOpen) setTimeout(() => abReviseInput?.focus(), 360);
  }

  function fallbackRejectGuard() {
    const btn = document.getElementById('btn-reject');
    if (!btn) return;
    if (btn.dataset.guard === '1') {
      fallbackBackToPhase1();
      return;
    }
    btn.dataset.guard = '1';
    setTimeout(() => { btn.dataset.guard = ''; }, 4000);
  }

  function fallbackBackToPhase1() {
    actionBar?.classList.remove('visible');
    abReviseWrap?.classList.remove('open');
    setTimeout(() => {
      phase3?.classList.remove('active');
      if (phase1) phase1.style.display = '';
      phase1Overlay?.classList.remove('active');
      composerWrap?.classList.remove('hidden');
      pfCard?.classList.remove('open');
      pfRowEls.forEach((r) => r.classList.remove('in'));
      if (phaseTag) phaseTag.textContent = 'phase-1';
      cinput?.focus();
    }, 350);
  }

  let rIdx = 0;
  const RESPONSES = [
    (t) => `понял — "${t.slice(0, 50)}". изучаю детали`,
    () => 'уточни: это при любом рестарте или только холодный старт?',
    () => 'хорошо, это уточняет приоритет задачи 02',
    () => 'принято — <em>намерение обновлено</em>'
  ];

  function fallbackLocalEcho(text) {
    if (!feed) return;
    const wrap = mk('bubble-wrap', '<div class="bubble">' + text + '</div>');
    feed.appendChild(wrap);
    feed.appendChild(mk('gap-sm'));
    feed.scrollTop = feed.scrollHeight;
    setTimeout(() => {
      const d = mk('doc-block');
      d.innerHTML = RESPONSES[Math.min(rIdx, RESPONSES.length - 1)](text);
      feed.appendChild(d);
      feed.appendChild(mk('gap'));
      feed.scrollTop = feed.scrollHeight;
      rIdx++;
    }, 700 + Math.random() * 400);
  }

  // ── Bootstrap ─────────────────────────────────────────────────
  wireActions();
  syncIntent();
  document.querySelector('.screen.active')?.classList.add('visible');

  if (bridge) {
    bridge.addEventListener('message', (event) => {
      const message = normalizeIncomingMessage(event.data);
      if (!message || message.type !== 'state_snapshot') {
        return;
      }
      render(message.payload);
    });
  }

  function signalReady() {
    emit({ type: 'dom_ready', payload: {} });
    emit({ type: 'renderer_ready', payload: {} });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', signalReady);
  } else {
    signalReady();
  }

  return { emit, render };
})();
