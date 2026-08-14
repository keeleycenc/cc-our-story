(function () {
  'use strict';

  /* 配色切换：和前台共用 localStorage 里的那一项，两边保持一致
     手机顶栏和 PC 顶栏各有一个开关，两个都要接上 */
  const root = document.documentElement;
  document.querySelectorAll('[data-theme-toggle]').forEach((toggle) => {
    toggle.addEventListener('click', () => {
      const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
      root.dataset.theme = next;
      try { localStorage.setItem('cc-color-mode', next); } catch (e) {}
    });
  });

  /* 手机底栏的「更多」：把不常用的入口收进一张从底部推上来的卡片 */
  const sheet = document.querySelector('[data-sheet]');
  if (sheet) {
    const openers = document.querySelectorAll('[data-sheet-open]');

    const setSheet = (open) => {
      sheet.classList.toggle('is-open', open);
      document.body.classList.toggle('sheet-open', open);
      openers.forEach((button) => button.setAttribute('aria-expanded', String(open)));
    };

    openers.forEach((button) => button.addEventListener('click', () => setSheet(!sheet.classList.contains('is-open'))));
    sheet.querySelectorAll('[data-sheet-close]').forEach((button) => button.addEventListener('click', () => setSheet(false)));

    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && sheet.classList.contains('is-open')) setSheet(false);
    });
  }

  const confirmDialog = (function () {
    let host = null;
    let titleNode = null;
    let textNode = null;
    let okButton = null;
    let cancelButton = null;
    let iconUse = null;
    let settle = null;
    let opener = null;

    const close = (answer) => {
      if (!host || !host.classList.contains('is-open')) return;
      host.classList.remove('is-open');
      document.body.classList.remove('sheet-open');

      const done = settle;
      settle = null;
      if (opener && document.contains(opener)) opener.focus();
      opener = null;
      if (done) done(answer);
    };

    const build = () => {
      host = document.createElement('div');
      host.className = 'admin-dialog';
      host.innerHTML =
        '<div class="admin-dialog-mask" data-dialog-cancel></div>' +
        '<div class="admin-dialog-card" role="alertdialog" aria-modal="true"' +
        ' aria-labelledby="admin-dialog-title" aria-describedby="admin-dialog-text">' +
        '<span class="admin-dialog-icon"><svg class="icon" aria-hidden="true"><use href="#i-trash-2"></use></svg></span>' +
        '<h2 class="admin-dialog-title" id="admin-dialog-title"></h2>' +
        '<p class="admin-dialog-text" id="admin-dialog-text"></p>' +
        '<div class="admin-dialog-actions">' +
        '<button class="btn btn-ghost" type="button" data-dialog-cancel>再想想</button>' +
        '<button class="btn btn-solid-danger" type="button" data-dialog-ok></button>' +
        '</div></div>';

      document.body.appendChild(host);

      titleNode = host.querySelector('.admin-dialog-title');
      textNode = host.querySelector('.admin-dialog-text');
      okButton = host.querySelector('[data-dialog-ok]');
      cancelButton = host.querySelector('.admin-dialog-actions [data-dialog-cancel]');
      iconUse = host.querySelector('.admin-dialog-icon use');

      host.querySelectorAll('[data-dialog-cancel]').forEach((node) => {
        node.addEventListener('click', () => close(false));
      });
      okButton.addEventListener('click', () => close(true));

      document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') close(false);
      });
    };

    return (options) => new Promise((done) => {
      if (!host) build();

      // 上一张还开着就当作放弃，避免两次询问叠在一起
      close(false);

      titleNode.textContent = options.title || '确定要删掉吗？';
      textNode.textContent = options.text || '';
      textNode.hidden = !options.text;
      okButton.textContent = options.ok || '删掉';
      cancelButton.textContent = options.cancel || '再想想';
      iconUse.setAttribute('href', '#i-' + (options.icon || 'trash-2'));
      host.classList.toggle('is-warning', options.tone === 'warning');
      okButton.className = options.danger === false ? 'btn' : 'btn btn-solid-danger';

      settle = done;
      opener = document.activeElement;
      host.classList.add('is-open');
      document.body.classList.add('sheet-open');
      okButton.focus();
    });
  }());

  document.querySelectorAll('form[data-confirm], form[data-confirm-title]').forEach((form) => {
    form.addEventListener('submit', (event) => {
      if (form.dataset.confirmed === 'yes') return;

      event.preventDefault();
      confirmDialog({
        title: form.dataset.confirmTitle,
        text: form.dataset.confirm,
        ok: form.dataset.confirmOk
      }).then((yes) => {
        if (!yes) return;
        form.dataset.confirmed = 'yes';
        if (form.requestSubmit) form.requestSubmit();
        else form.submit();
      });
    });
  });

  /* 编辑页离开保护：站内跳转统一使用自己的确认框，不再调用浏览器原生提示。 */
  document.querySelectorAll('form[data-dirty-guard]').forEach((form) => {
    let dirty = false;
    let submitting = false;

    const markDirty = () => { dirty = true; };
    form.addEventListener('input', markDirty);
    form.addEventListener('change', markDirty);
    form.addEventListener('submit', () => {
      submitting = true;
      dirty = false;
    });

    document.addEventListener('click', (event) => {
      if (!dirty || submitting || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const link = event.target.closest('a[href]');
      if (!link || link.target === '_blank' || link.hasAttribute('download')) return;

      const destination = new URL(link.href, window.location.href);
      if (destination.pathname === window.location.pathname && destination.search === window.location.search && destination.hash) return;

      event.preventDefault();
      confirmDialog({
        title: '更改尚未保存',
        text: '现在离开后，本页刚刚做的修改将不会保存。',
        ok: '不保存并离开',
        cancel: '继续编辑',
        icon: 'circle-alert',
        tone: 'warning',
        danger: false
      }).then((yes) => {
        if (!yes) return;
        dirty = false;
        window.location.assign(destination.href);
      });
    });
  });

  /* 留言正文折起来：CSS 里先按四行截断，这里只负责判断有没有被截到 ——
     没截到就不摆按钮，免得短短一句话下面还挂个没用的「展开」 */
  document.querySelectorAll('[data-clamp]').forEach((text) => {
    const toggle = text.parentNode.querySelector('[data-clamp-toggle]');
    if (!toggle) return;

    if (text.scrollHeight <= text.clientHeight + 2) return;

    const label = toggle.querySelector('span') || toggle;
    toggle.hidden = false;

    toggle.addEventListener('click', () => {
      const open = text.classList.toggle('is-open');
      toggle.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', String(open));
      label.textContent = open ? '收起' : '展开';
    });

    toggle.setAttribute('aria-expanded', 'false');
  });

  /* 图片占位：OSS 和外链有时要等上好几秒，先摆一块骨架屏把位置占住，
     图到了再淡入；实在拿不到就换成一句提示，不留一块空白 */
  document.querySelectorAll('[data-thumb]').forEach((thumb) => {
    const image = thumb.querySelector('img');
    if (!image) return;

    const mark = (state) => thumb.classList.add(state);

    // 脚本跑起来时图可能已经在缓存里了，这种情况下 load 不会再触发
    if (image.complete) {
      mark(image.naturalWidth > 0 ? 'is-ready' : 'is-failed');
      return;
    }

    image.addEventListener('load', () => mark('is-ready'));
    image.addEventListener('error', () => mark('is-failed'));
  });

  /* 图片库里的「复制链接」：文件名那一行放不下完整地址，靠这个按钮取 */
  document.querySelectorAll('[data-copy]').forEach((button) => {
    const label = button.querySelector('span') || button;
    const original = label.textContent;

    button.addEventListener('click', async () => {
      const text = button.dataset.copy;
      let ok = true;

      // 局域网里常常是 http，没有 clipboard API，退回到老办法
      if (navigator.clipboard && window.isSecureContext) {
        try { await navigator.clipboard.writeText(text); } catch (e) { ok = false; }
      } else {
        const holder = document.createElement('textarea');
        holder.value = text;
        holder.setAttribute('readonly', '');
        holder.style.position = 'fixed';
        holder.style.opacity = '0';
        document.body.appendChild(holder);
        holder.select();
        try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
        document.body.removeChild(holder);
      }

      label.textContent = ok ? '已复制' : '复制不了，手动选一下';
      setTimeout(() => { label.textContent = original; }, 1600);
    });
  });

  /* 点点滴滴和纪念日共用同一套 Markdown、预览和插图行为。 */
  document.querySelectorAll('[data-markdown-composer]').forEach((composer) => {
    const editor = composer.querySelector('[data-markdown-editor]');
    const editorPane = composer.querySelector('[data-markdown-editor-pane]');
    const previewPane = composer.querySelector('[data-markdown-preview-pane]');
    const editButton = composer.querySelector('[data-markdown-edit]');
    const previewButton = composer.querySelector('[data-markdown-preview-button]');
    const uploadInput = composer.querySelector('[data-markdown-upload]');
    const status = composer.querySelector('[data-markdown-status]');
    const cover = composer.querySelector('.markdown-cover-field input');
    let renderedValue = null;

    if (!editor || !editorPane || !previewPane) return;

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]');
    const setMode = (previewing) => {
      editorPane.hidden = previewing;
      previewPane.hidden = !previewing;
      editButton.classList.toggle('is-active', !previewing);
      previewButton.classList.toggle('is-active', previewing);
      editButton.setAttribute('aria-selected', String(!previewing));
      previewButton.setAttribute('aria-selected', String(previewing));
    };

    editButton.addEventListener('click', () => {
      setMode(false);
      editor.focus();
    });

    previewButton.addEventListener('click', async () => {
      if (renderedValue === editor.value) {
        setMode(true);
        return;
      }

      const body = new FormData();
      body.append('content', editor.value);
      const verification = token();
      if (verification) body.append('__RequestVerificationToken', verification.value);
      status.textContent = '正在生成预览…';

      try {
        const response = await fetch('/admin/media?handler=Preview', { method: 'POST', body: body });
        const data = await response.json();
        if (!response.ok || !data.ok) throw new Error('preview failed');

        previewPane.innerHTML = data.html || '<p class="markdown-preview-empty">写下一些内容后，在这里查看最终排版。</p>';
        renderedValue = editor.value;
        setMode(true);
        status.textContent = '预览已更新。';
      } catch (error) {
        status.textContent = '预览暂时生成不了，请稍后重试。';
      }
    });

    if (!uploadInput) return;
    uploadInput.addEventListener('change', async () => {
      const file = uploadInput.files && uploadInput.files[0];
      if (!file) return;

      const body = new FormData();
      body.append('file', file);
      const verification = token();
      if (verification) body.append('__RequestVerificationToken', verification.value);
      status.textContent = '正在上传…';

      try {
        const response = await fetch('/admin/media?handler=Upload', { method: 'POST', body: body });
        const data = await response.json();
        if (!response.ok || !data.ok) {
          status.textContent = data.error || '上传失败。';
          return;
        }

        const snippet = '\n![图片](' + data.url + ')\n';
        const at = editor.selectionStart || editor.value.length;
        editor.value = editor.value.slice(0, at) + snippet + editor.value.slice(at);
        editor.selectionStart = editor.selectionEnd = at + snippet.length;
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        if (cover && !cover.value) cover.value = data.url;
        renderedValue = null;
        setMode(false);
        editor.focus();
        status.textContent = '图片已插入正文。';
      } catch (error) {
        status.textContent = '网络开了个小差，稍后再试。';
      } finally {
        uploadInput.value = '';
      }
    });
  });
}());
