const zavodBridge = (() => {
  const bridge = window.chrome?.webview ?? null;
  if (!bridge) {
    return null;
  }

  const input = document.querySelector('.input');
  const modeSwitch = document.querySelector('.mode-switch');
  const sendBtn = document.querySelector('.icon-right');
  const plusBtn = document.querySelector('.icon-left');
  const page = document.querySelector('.page');
  const composer = document.querySelector('.composer');
  const composerInner = document.querySelector('.composer-inner');
  const conversation = document.querySelector('.conversation');
  const fileInput = document.querySelector('.file-input');
  const plusMenu = document.querySelector('.plus-menu');
  const plusItems = document.querySelectorAll('.plus-item');
  const attachmentsEl = document.querySelector('.attachments');
  const appEl = document.querySelector('.app');
  const sidebarToggle = document.querySelector('.sidebar-toggle');
  const chatListEl = document.querySelector('.chat-list');
  const historyLoadRow = document.querySelector('.history-load-row');
  const historyLoadButton = document.querySelector('.history-load-button');

  let renderedState = null;
  let renderedConversationKey = null;
  const messageStore = new Map();
  const messageNodes = new Map();
  let orderedMessageIds = [];
  let patchPass = 0;
  let olderRequestPending = false;
  let scrollChromeTimer = null;
  let localizedText = {
    sidebarShow: '',
    sidebarHide: '',
    newChat: '',
    addAttachmentTitle: '',
    sendTitle: '',
    addFile: '',
    addImage: '',
    addNote: '',
    loadOlder: '',
    loadingOlder: '',
    referenceSeparator: ' • ',
    defaultArtifactLabel: '',
    defaultLogLabel: ''
  };

  function emit(message) {
    if (!message || !message.type) {
      return;
    }

    bridge.postMessage(message);
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

  function auditLayoutState(label = 'layout') {
    const pageEl = page;
    const composerInnerEl = composerInner;
    const topbarContentEl = document.querySelector('.mode-switch:not([hidden])');

    if (!pageEl || !composerInnerEl) {
      return { ok: false, reason: 'missing elements' };
    }

    const pageRect = pageEl.getBoundingClientRect();
    const composerRect = composerInnerEl.getBoundingClientRect();
    const topbarRect = topbarContentEl?.getBoundingClientRect() ?? null;
    const firstDocEl = document.querySelector('.message-doc');
    const firstDocRect = firstDocEl ? firstDocEl.getBoundingClientRect() : null;

    const pageCenter = pageRect.left + pageRect.width / 2;
    const composerCenter = composerRect.left + composerRect.width / 2;

    const checks = [
      ['composer center', Math.abs(pageCenter - composerCenter) <= 1],
      ['composer width', Math.abs(pageRect.width - composerRect.width) <= 1],
      ['message-doc width', !firstDocRect || Math.abs(pageRect.width - firstDocRect.width) <= 1],
      ['message-doc center', !firstDocRect || Math.abs(pageCenter - (firstDocRect.left + firstDocRect.width / 2)) <= 1]
    ];

    if (topbarRect && topbarRect.width > 0) {
      const topbarCenter = topbarRect.left + topbarRect.width / 2;
      checks.push(['topbar center', Math.abs(pageCenter - topbarCenter) <= 1]);
    }

    return {
      ok: checks.every(([, ok]) => ok),
      failed: checks.filter(([, ok]) => !ok).map(([name]) => name)
    };
  }

  function syncSidebarToggle() {
    const isCollapsed = appEl.classList.contains('collapsed');
    const title = isCollapsed
      ? localizedText.sidebarShow
      : localizedText.sidebarHide;

    sidebarToggle.title = title;
    sidebarToggle.setAttribute('aria-label', title);
    sidebarToggle.setAttribute('aria-expanded', String(!isCollapsed));
  }

  function applyLocalizedText(text) {
    localizedText = {
      ...localizedText,
      ...(text || {})
    };

    const fileLabel = document.querySelector('.plus-item-file');
    const imageLabel = document.querySelector('.plus-item-image');
    const noteLabel = document.querySelector('.plus-item-note');
    if (fileLabel) {
      fileLabel.textContent = localizedText.addFile;
    }
    if (imageLabel) {
      imageLabel.textContent = localizedText.addImage;
    }
    if (noteLabel) {
      noteLabel.textContent = localizedText.addNote;
    }

    plusBtn.title = localizedText.addAttachmentTitle;
    plusBtn.setAttribute('aria-label', localizedText.addAttachmentTitle);
    sendBtn.title = localizedText.sendTitle;
    sendBtn.setAttribute('aria-label', localizedText.sendTitle);
    input.placeholder = renderedState?.composer?.placeholder || input.placeholder;
    updateHistoryLoadState(renderedState);
    syncSidebarToggle();
  }

  function setEmptyState(isEmpty) {
    appEl.classList.toggle('is-empty', Boolean(isEmpty));
    requestAnimationFrame(() => auditLayoutState(isEmpty ? 'empty' : 'filled'));
  }

  function syncComposerMetrics() {
    if (!page || !composer || !composerInner) {
      return;
    }

    const composerRect = composerInner.getBoundingClientRect();
    const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
    const visibleComposerHeight = Math.max(0, viewportHeight - composerRect.top);
    const safeSpace = Math.max(220, Math.ceil(visibleComposerHeight + 32));
    document.documentElement.style.setProperty('--composer-safe-space', `${safeSpace}px`);
  }

  function pulseScrollChrome() {
    if (!page) {
      return;
    }

    page.classList.add('is-scrolling');
    if (scrollChromeTimer !== null) {
      window.clearTimeout(scrollChromeTimer);
    }

    scrollChromeTimer = window.setTimeout(() => {
      page.classList.remove('is-scrolling');
      scrollChromeTimer = null;
    }, 900);
  }

  function renderChats(chats, activeChatId) {
    chatListEl.innerHTML = '';

    const newChat = document.createElement('div');
    newChat.className = 'chat-item new';
    newChat.dataset.chatId = 'draft';
    newChat.textContent = localizedText.newChat;
    chatListEl.appendChild(newChat);

    (chats || []).forEach((chat) => {
      const item = document.createElement('div');
      item.className = 'chat-item';
      item.dataset.chatId = chat.id;
      item.textContent = chat.title;
      item.classList.toggle('active', chat.id === activeChatId);
      chatListEl.appendChild(item);
    });
  }

  function renderAssistantMessage(message) {
    const body = document.createElement('div');
    body.className = 'message-doc-body';
    const paragraphs = String(message.text ?? '')
      .split(/\n\s*\n/g)
      .map((part) => part.trim())
      .filter(Boolean);

    if (paragraphs.length === 0) {
      const p = document.createElement('p');
      p.textContent = '';
      body.appendChild(p);
    } else {
      paragraphs.forEach((paragraph) => {
        const p = document.createElement('p');
        appendPlainTextWithBreaks(p, paragraph);
        body.appendChild(p);
      });
    }

    return body;
  }

  function appendPlainTextWithBreaks(node, text) {
    const lines = String(text ?? '').split('\n');
    lines.forEach((line, index) => {
      if (index > 0) {
        node.appendChild(document.createElement('br'));
      }
      node.appendChild(document.createTextNode(line));
    });
  }

  function renderStatusMessage(message) {
    const row = document.createElement('div');
    row.className = 'message-status';
    row.textContent = message.text ?? '';
    return row;
  }

  function renderLogMessage(message) {
    const row = document.createElement('div');
    row.className = 'message-log';

    if (message.label || message.referenceId) {
      const label = document.createElement('div');
      label.className = 'message-artifact-label';
      label.textContent = `${message.label || localizedText.defaultLogLabel}${message.referenceId ? `${localizedText.referenceSeparator}${message.referenceId}` : ''}`;
      row.appendChild(label);
    }

    const text = document.createElement('div');
    text.textContent = message.text ?? '';
    row.appendChild(text);
    return row;
  }

  function renderArtifactMessage(message) {
    const row = document.createElement('div');
    row.className = 'message-artifact';

    const label = document.createElement('div');
    label.className = 'message-artifact-label';
    label.textContent = `${message.label || localizedText.defaultArtifactLabel}${message.referenceId ? `${localizedText.referenceSeparator}${message.referenceId}` : ''}`;

    const text = document.createElement('div');
    text.className = 'message-artifact-text';
    text.textContent = message.text ?? '';

    row.appendChild(label);
    row.appendChild(text);
    return row;
  }

  function normalizeConversationItem(message) {
    if (!message || !message.id) {
      return null;
    }

    const revision = Number.isFinite(message.revision) ? Number(message.revision) : parseInt(message.revision || '1', 10);
    return {
      id: String(message.id),
      revision: Number.isFinite(revision) && revision > 0 ? revision : 1,
      role: message.role === 'user' || message.role === 'system' ? message.role : 'assistant',
      kind: message.kind === 'status' || message.kind === 'log' || message.kind === 'artifact'
        ? message.kind
        : 'message',
      format: message.format === 'plain' ? 'plain' : 'markdown',
      text: String(message.text ?? ''),
      label: typeof message.label === 'string' && message.label.trim().length > 0 ? message.label.trim() : null,
      referenceId: typeof message.referenceId === 'string' && message.referenceId.trim().length > 0 ? message.referenceId.trim() : null,
      streamState: message.streamState === 'streaming' || message.streamState === 'idle' || message.streamState === 'error'
        ? message.streamState
        : 'final'
    };
  }

  function buildConversationKey(state) {
    return `${state?.mode || 'chats'}:${state?.activeChatId || 'draft'}`;
  }

  function createMessageNode(message) {
    const node = document.createElement('div');
    node.dataset.messageId = message.id;
    applyMessageNode(node, message);
    return node;
  }

  function applyMessageNode(node, message) {
    node.dataset.messageId = message.id;
    node.dataset.revision = String(message.revision);
    node.dataset.role = message.role;
    node.dataset.kind = message.kind;
    node.dataset.format = message.format;
    node.dataset.streamState = message.streamState;

    if (message.kind === 'message' && message.role === 'user') {
      node.className = 'message-user';
      node.hidden = false;
      node.textContent = message.text;
      return;
    }

    if (message.kind === 'status') {
      node.className = 'message-row message-row-status';
      node.hidden = false;
      node.replaceChildren(renderStatusMessage(message));
      return;
    }

    if (message.kind === 'log') {
      node.className = 'message-row message-row-log';
      node.hidden = false;
      node.replaceChildren(renderLogMessage(message));
      return;
    }

    if (message.kind === 'artifact') {
      node.className = 'message-row message-row-artifact';
      node.hidden = false;
      node.replaceChildren(renderArtifactMessage(message));
      return;
    }

    node.className = 'message-doc';
    node.hidden = message.streamState === 'streaming' && message.text.trim().length === 0;
    node.replaceChildren(renderAssistantMessage(message));
  }

  function emitPatchStats(payload) {
    emit({
      type: 'conversation_patch_stats',
      payload
    });
  }

  function emitWindowingStats(payload) {
    emit({
      type: 'windowing_stats',
      payload
    });
  }

  function resetConversation(messages, conversationKey) {
    const fragment = document.createDocumentFragment();
    const normalizedMessages = [];

    messageStore.clear();
    messageNodes.clear();
    orderedMessageIds = [];

    (messages || []).forEach((message) => {
      const normalized = normalizeConversationItem(message);
      if (!normalized) {
        return;
      }

      normalizedMessages.push(normalized);
      orderedMessageIds.push(normalized.id);
      messageStore.set(normalized.id, normalized);

      const node = createMessageNode(normalized);
      messageNodes.set(normalized.id, node);
      fragment.appendChild(node);
    });

    conversation.replaceChildren(fragment);
    renderedConversationKey = conversationKey;
    patchPass += 1;
    emitPatchStats({
      pass: patchPass,
      mode: 'reset',
      conversationKey,
      fullReset: true,
      createdCount: normalizedMessages.length,
      updatedCount: 0,
      removedCount: 0,
      ignoredCount: 0,
      movedCount: 0,
      nodeCount: conversation.childElementCount,
      createdIds: normalizedMessages.map((message) => message.id),
      createdKinds: normalizedMessages.map((message) => message.kind),
      updatedIds: [],
      ignoredIds: []
    });
  }

  function patchConversation(messages, conversationKey) {
    const normalizedMessages = [];
    const nextIds = new Set();
    let createdCount = 0;
    let updatedCount = 0;
    let removedCount = 0;
    let ignoredCount = 0;
    let movedCount = 0;
    const createdIds = [];
    const updatedIds = [];
    const ignoredIds = [];

    (messages || []).forEach((message) => {
      const normalized = normalizeConversationItem(message);
      if (!normalized) {
        return;
      }

      normalizedMessages.push(normalized);
      nextIds.add(normalized.id);
    });

    normalizedMessages.forEach((message, index) => {
      const existing = messageStore.get(message.id) || null;
      let node = messageNodes.get(message.id) || null;
      let applied = existing;

      if (!existing || !node) {
        node = createMessageNode(message);
        messageNodes.set(message.id, node);
        messageStore.set(message.id, message);
        createdCount += 1;
        createdIds.push(message.id);
        applied = message;
      } else if (message.revision > existing.revision) {
        applyMessageNode(node, message);
        messageStore.set(message.id, message);
        updatedCount += 1;
        updatedIds.push(message.id);
        applied = message;
      } else {
        ignoredCount += 1;
        ignoredIds.push(message.id);
      }

      const currentNodeAtIndex = conversation.children[index] || null;
      if (currentNodeAtIndex !== node) {
        conversation.insertBefore(node, currentNodeAtIndex);
        movedCount += 1;
      }

      normalizedMessages[index] = applied;
    });

    orderedMessageIds
      .filter((id) => !nextIds.has(id))
      .forEach((id) => {
        const node = messageNodes.get(id);
        if (node && node.parentNode === conversation) {
          conversation.removeChild(node);
        }

        messageNodes.delete(id);
        messageStore.delete(id);
        removedCount += 1;
      });

    orderedMessageIds = normalizedMessages.map((message) => message.id);
    renderedConversationKey = conversationKey;
    patchPass += 1;
    emitPatchStats({
      pass: patchPass,
      mode: 'patch',
      conversationKey,
      fullReset: false,
      createdCount,
      updatedCount,
      removedCount,
      ignoredCount,
      movedCount,
      nodeCount: conversation.childElementCount,
      createdIds,
      createdKinds: createdIds.map((id) => messageStore.get(id)?.kind ?? 'unknown'),
      updatedIds,
      updatedKinds: updatedIds.map((id) => messageStore.get(id)?.kind ?? 'unknown'),
      ignoredIds
    });
  }

  function renderAttachments(items) {
    attachmentsEl.innerHTML = '';
    const attachments = Array.isArray(items) ? items : [];
    attachmentsEl.hidden = attachments.length === 0;

    attachments.forEach((file) => {
      const chip = document.createElement('div');
      chip.className = 'attachment-chip';

      const name = document.createElement('span');
      name.textContent = file.detail
        ? `${file.label} (${file.detail})`
        : file.label;

      const removeBtn = document.createElement('button');
      removeBtn.type = 'button';
      removeBtn.textContent = '\u00d7';
      removeBtn.addEventListener('click', () => {
        emit({
          type: 'remove_attachment',
          payload: { draftId: file.id }
        });
      });

      chip.appendChild(name);
      chip.appendChild(removeBtn);
      attachmentsEl.appendChild(chip);
    });

    syncComposerMetrics();
  }

  function updateHistoryLoadState(state) {
    const hasOlder = Boolean(state?.hasOlder) && !Boolean(state?.isEmpty);
    historyLoadRow.hidden = !hasOlder;
    historyLoadButton.disabled = !hasOlder || olderRequestPending;
    historyLoadButton.textContent = olderRequestPending ? localizedText.loadingOlder : localizedText.loadOlder;
  }

  function requestOlder() {
    if (!renderedState || !renderedState.hasOlder || olderRequestPending) {
      return;
    }

    olderRequestPending = true;
    updateHistoryLoadState(renderedState);
    emitWindowingStats({
      action: 'request_older',
      beforeSeq: renderedState.windowStartSeq,
      windowStartSeq: renderedState.windowStartSeq,
      windowEndSeq: renderedState.windowEndSeq
    });
    emit({
      type: 'request_older',
      payload: { beforeSeq: renderedState.windowStartSeq }
    });
  }

  function render(state) {
    if (!state) {
      return;
    }

    const conversationKey = buildConversationKey(state);
    const shouldReset = renderedState === null || renderedConversationKey !== conversationKey;
    const isPrepend = !shouldReset
      && Number.isFinite(renderedState?.windowStartSeq)
      && Number.isFinite(state.windowStartSeq)
      && state.windowStartSeq > 0
      && state.windowStartSeq < renderedState.windowStartSeq;
    const scroller = page || document.scrollingElement || document.documentElement;
    const previousScrollHeight = isPrepend ? scroller.scrollHeight : 0;
    const previousScrollTop = isPrepend ? scroller.scrollTop : 0;
    const previousWindowStartSeq = renderedState?.windowStartSeq ?? 0;

    renderedState = state;
    olderRequestPending = false;
    applyLocalizedText(state.text);
    document.body.dataset.mode = state.mode || 'chats';
    if (modeSwitch) {
      modeSwitch.dataset.active = state.mode || 'chats';
    }
    input.placeholder = state.composer?.placeholder || input.placeholder;

    const headlineEl = document.querySelector('.empty-title');
    const subtitleEl = document.querySelector('.empty-subtitle');

    if (headlineEl && state.emptyState?.headline) {
      headlineEl.textContent = state.emptyState.headline;
    }

    if (subtitleEl && state.emptyState?.subtitle) {
      subtitleEl.textContent = state.emptyState.subtitle;
    }

    renderChats(state.chats, state.activeChatId);
    renderAttachments(state.composer?.pendingAttachments || []);
    updateHistoryLoadState(state);
    if (shouldReset) {
      resetConversation(state.messages, conversationKey);
    } else {
      patchConversation(state.messages, conversationKey);
    }
    setEmptyState(state.isEmpty);
    syncComposerMetrics();

    requestAnimationFrame(() => {
      if (isPrepend) {
        const heightDelta = scroller.scrollHeight - previousScrollHeight;
        scroller.scrollTop = previousScrollTop + heightDelta;
        emitWindowingStats({
          action: 'prepend',
          fullReset: false,
          beforeWindowStartSeq: previousWindowStartSeq,
          windowStartSeq: state.windowStartSeq,
          windowEndSeq: state.windowEndSeq,
          hasOlder: state.hasOlder,
          heightDelta,
          previousScrollTop,
          adjustedScrollTop: scroller.scrollTop
        });
      } else {
        emitWindowingStats({
          action: shouldReset ? 'reset' : 'patch',
          fullReset: shouldReset,
          windowStartSeq: state.windowStartSeq,
          windowEndSeq: state.windowEndSeq,
          hasOlder: state.hasOlder,
          loadedCount: Array.isArray(state.messages) ? state.messages.length : 0
        });
      }

      syncComposerMetrics();
      auditLayoutState(state.isEmpty ? 'snapshot-empty' : 'snapshot-filled');
      emit({
        type: 'render_complete',
        payload: {}
      });
    });
  }

  function submitComposer() {
    const text = input.value.trim();
    const pendingAttachments = renderedState?.composer?.pendingAttachments || [];
    if (!text && pendingAttachments.length === 0) {
      return;
    }

    emit({
      type: 'send_message',
      payload: { text }
    });

    input.value = '';
  }

  sendBtn.addEventListener('click', submitComposer);
  sendBtn.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      submitComposer();
    }
  });

  plusBtn.addEventListener('click', () => {
    plusMenu.hidden = !plusMenu.hidden;
  });

  historyLoadButton.addEventListener('click', () => {
    requestOlder();
  });

  page?.addEventListener('scroll', () => {
    pulseScrollChrome();
  }, { passive: true });

  plusBtn.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      plusMenu.hidden = !plusMenu.hidden;
    }
  });

  input.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      submitComposer();
    }
  });

  input.addEventListener('paste', (event) => {
    const pastedText = event.clipboardData?.getData('text/plain') || '';
    const lineCount = pastedText.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n').length;
    if (pastedText.length <= 4000 && lineCount <= 40) {
      return;
    }

    event.preventDefault();
    emit({
      type: 'stage_text_artifact',
      payload: { text: pastedText }
    });
  });

  input.addEventListener('click', () => {
    plusMenu.hidden = true;
  });

  chatListEl.addEventListener('click', (event) => {
    const item = event.target.closest('.chat-item[data-chat-id]');
    if (!item) {
      return;
    }

    if (item.classList.contains('new')) {
      emit({ type: 'new_chat', payload: {} });
      return;
    }

    const chatId = item.dataset.chatId;
    if (!chatId || chatId === renderedState?.activeChatId) {
      return;
    }

    emit({
      type: 'select_chat',
      payload: { chatId }
    });
  });

  plusItems.forEach((item) => {
    item.addEventListener('click', () => {
      const type = item.dataset.type;

      if (type === 'file' || type === 'image') {
        emit({
          type: 'request_attach_files',
          payload: { sourceType: type }
        });
      }

      if (type === 'note') {
        input.focus();
      }

      plusMenu.hidden = true;
    });
  });

  if (modeSwitch) {
    modeSwitch.addEventListener('click', (event) => {
      const target = event.target.closest('.mode-pill');
      const currentMode = modeSwitch.dataset.active || 'chats';
      const nextMode = target?.dataset.mode || (currentMode === 'chats' ? 'projects' : 'chats');

      if (nextMode === currentMode) {
        return;
      }

      emit({
        type: 'toggle_mode',
        payload: { mode: nextMode }
      });
    });
  }

  document.addEventListener('click', (event) => {
    if (!event.target.closest('.composer-inner')) {
      plusMenu.hidden = true;
    }
  });

  sidebarToggle.addEventListener('click', () => {
    appEl.classList.toggle('collapsed');
    syncSidebarToggle();
    requestAnimationFrame(() => {
      auditLayoutState(appEl.classList.contains('is-empty') ? 'collapsed-empty-toggle' : 'collapsed-filled-toggle');
    });
  });

  window.addEventListener('resize', () => {
    syncComposerMetrics();
    auditLayoutState(appEl.classList.contains('is-empty') ? 'resize-empty' : 'resize-filled');
  });

  window.addEventListener('load', () => {
    syncComposerMetrics();
    syncSidebarToggle();
  });

  bridge.addEventListener('message', (event) => {
    const message = normalizeIncomingMessage(event.data);
    if (!message || message.type !== 'state_snapshot') {
      return;
    }

    render(message.payload);
  });

  requestAnimationFrame(() => {
    syncComposerMetrics();
    syncSidebarToggle();
  });

  emit({
    type: 'dom_ready',
    payload: {}
  });

  emit({
    type: 'renderer_ready',
    payload: {}
  });

  return {
    emit,
    render
  };
})();






