/*
 * 图片查看器。
 *
 * 默认展示优化后的预览图，打开时按需加载当前原图，避免初始化加载全部高清资源。
 *
 * 通过 data-lightbox 分组、data-full 指定原图地址即可复用；
 * 支持正文图片、相册、时间轴等场景。
 *
 * 也可通过 window.ccLightbox.open(items, index) 手动调用。
 */
(function () {
  'use strict';

  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const MAX_SCALE = 5;
  const MIN_SCALE = 1;

  let overlay = null;      // 整个遮罩层，第一次打开时才建
  let parts = null;        // 里面那几个常用节点
  let items = [];          // 当前这一组的全部图片
  let current = 0;
  let scale = 1;
  let offsetX = 0;
  let offsetY = 0;
  let lastFocused = null;
  let requestId = 0;       // 每次换图 +1，回来的旧图片就知道自己已经过时了

  const icon = (name) => '<svg class="icon" aria-hidden="true"><use href="#i-' + name + '"></use></svg>';

  const build = () => {
    const node = document.createElement('div');
    node.className = 'lightbox';
    node.id = 'lightbox';
    node.hidden = true;
    node.setAttribute('role', 'dialog');
    node.setAttribute('aria-modal', 'true');
    node.setAttribute('aria-label', '图片查看器');
    node.innerHTML =
      '<div class="lightbox-bar">' +
        '<span class="lightbox-count" aria-live="polite"></span>' +
        '<span class="lightbox-tools">' +
          '<button type="button" class="lightbox-button" data-act="out" aria-label="缩小">' + icon('zoom-out') + '</button>' +
          '<button type="button" class="lightbox-button" data-act="in" aria-label="放大">' + icon('zoom-in') + '</button>' +
          '<button type="button" class="lightbox-button" data-act="close" aria-label="关闭查看器">' + icon('x') + '</button>' +
        '</span>' +
      '</div>' +
      '<button type="button" class="lightbox-nav is-prev" data-act="prev" aria-label="上一张">' + icon('chevron-left') + '</button>' +
      '<button type="button" class="lightbox-nav is-next" data-act="next" aria-label="下一张">' + icon('chevron-right') + '</button>' +
      '<div class="lightbox-stage" data-stage>' +
        '<div class="lightbox-canvas" data-canvas>' +
          '<img class="lightbox-thumb" alt="" aria-hidden="true" data-thumb>' +
          '<img class="lightbox-full" alt="" data-full>' +
        '</div>' +
        '<span class="lightbox-spinner" data-spinner aria-hidden="true"></span>' +
        '<p class="lightbox-failed" data-failed hidden>' + icon('image-off') + '这张图没能加载出来</p>' +
      '</div>' +
      '<p class="lightbox-caption" data-caption></p>';

    document.body.append(node);

    parts = {
      stage: node.querySelector('[data-stage]'),
      canvas: node.querySelector('[data-canvas]'),
      thumb: node.querySelector('[data-thumb]'),
      full: node.querySelector('[data-full]'),
      spinner: node.querySelector('[data-spinner]'),
      failed: node.querySelector('[data-failed]'),
      caption: node.querySelector('[data-caption]'),
      count: node.querySelector('.lightbox-count'),
      prev: node.querySelector('[data-act="prev"]'),
      next: node.querySelector('[data-act="next"]')
    };

    bind(node);
    return node;
  };

  /* ---- 变换：缩放和平移合成一句 transform ---- */

  const clampOffset = () => {
    // 缩放之后图片比舞台大出来的部分，就是能拖动的范围
    const stage = parts.stage.getBoundingClientRect();
    const width = parts.canvas.offsetWidth * scale;
    const height = parts.canvas.offsetHeight * scale;
    const roomX = Math.max(0, (width - stage.width) / 2);
    const roomY = Math.max(0, (height - stage.height) / 2);

    offsetX = Math.min(roomX, Math.max(-roomX, offsetX));
    offsetY = Math.min(roomY, Math.max(-roomY, offsetY));
  };

  const applyTransform = (animate) => {
    clampOffset();
    parts.canvas.style.transition = animate && !reduceMotion ? 'transform .22s ease' : 'none';
    parts.canvas.style.transform = 'translate(' + offsetX + 'px,' + offsetY + 'px) scale(' + scale + ')';
    overlay.classList.toggle('is-zoomed', scale > 1.001);
  };

  const resetTransform = () => {
    scale = 1;
    offsetX = 0;
    offsetY = 0;
    applyTransform(false);
  };

  // origin 是以舞台中心为原点的坐标，围着它缩放，手指/指针底下的那一点才不会跑
  const zoomTo = (next, origin, animate) => {
    const target = Math.min(MAX_SCALE, Math.max(MIN_SCALE, next));
    if (Math.abs(target - scale) < 0.001) return;

    if (origin) {
      const ratio = target / scale;
      offsetX = origin.x - ((origin.x - offsetX) * ratio);
      offsetY = origin.y - ((origin.y - offsetY) * ratio);
    }

    scale = target;
    if (target === MIN_SCALE) {
      offsetX = 0;
      offsetY = 0;
    }

    applyTransform(animate !== false);
  };

  const stageOrigin = (clientX, clientY) => {
    const box = parts.stage.getBoundingClientRect();
    return { x: clientX - box.left - (box.width / 2), y: clientY - box.top - (box.height / 2) };
  };

  /* ---- 换图 ---- */

  const show = (index) => {
    if (!items.length) return;

    current = (index + items.length) % items.length;
    const item = items[current];
    const ticket = ++requestId;

    resetTransform();
    overlay.classList.remove('is-loaded', 'is-failed');
    parts.failed.hidden = true;

    // 页面上那张压过的小图先顶上，原图到了再换掉，中间不会是一片黑
    if (item.preview) parts.thumb.src = item.preview;
    else parts.thumb.removeAttribute('src');
    parts.thumb.hidden = !item.preview;
    parts.full.removeAttribute('src');
    parts.full.alt = item.alt || '';

    parts.caption.textContent = item.caption || '';
    parts.caption.hidden = !item.caption;
    parts.count.textContent = items.length > 1 ? (current + 1) + ' / ' + items.length : '';
    parts.prev.hidden = items.length < 2;
    parts.next.hidden = items.length < 2;

    const loader = new Image();
    loader.decoding = 'async';

    loader.onload = () => {
      if (ticket !== requestId) return;       // 已经翻到别的图了，这张不要了
      parts.full.src = item.full;
      overlay.classList.add('is-loaded');
      preloadNeighbours();
    };

    loader.onerror = () => {
      if (ticket !== requestId) return;
      overlay.classList.add('is-failed');
      parts.failed.hidden = false;
    };

    loader.src = item.full;
  };

  // 当前这张读完了才顺手预取前后各一张，不是一上来就全拉
  const preloadNeighbours = () => {
    if (items.length < 2) return;
    if (navigator.connection && navigator.connection.saveData) return;

    const run = () => {
      [current - 1, current + 1].forEach((index) => {
        const item = items[(index + items.length) % items.length];
        if (!item || item.preloaded) return;
        item.preloaded = true;
        const image = new Image();
        image.decoding = 'async';
        image.src = item.full;
      });
    };

    if (window.requestIdleCallback) window.requestIdleCallback(run, { timeout: 2000 });
    else window.setTimeout(run, 400);
  };

  const step = (delta) => {
    if (items.length > 1) show(current + delta);
  };

  /* ---- 开关 ---- */

  const close = () => {
    if (!overlay || overlay.hidden) return;

    requestId++;
    overlay.hidden = true;
    document.body.classList.remove('modal-open');
    parts.full.removeAttribute('src');
    parts.thumb.removeAttribute('src');
    items = [];

    if (lastFocused && document.contains(lastFocused)) lastFocused.focus();
    lastFocused = null;
  };

  const open = (list, index) => {
    const usable = (list || []).filter((item) => item && item.full);
    if (!usable.length) return;

    overlay = overlay || build();
    lastFocused = document.activeElement;
    items = usable.map((item) => Object.assign({}, item));

    overlay.hidden = false;
    document.body.classList.add('modal-open');
    show(index || 0);
    overlay.querySelector('[data-act="close"]').focus();
  };

  /* ---- 交互 ---- */

  const bind = (node) => {
    // 按钮只认 click：指针那套在 pointerdown 里就把它们放过去了
    node.addEventListener('click', (event) => {
      const button = event.target.closest('[data-act]');
      if (!button) return;

      const act = button.dataset.act;
      if (act === 'close') close();
      else if (act === 'prev') step(-1);
      else if (act === 'next') step(1);
      else if (act === 'in') zoomTo(scale * 1.5, null);
      else if (act === 'out') zoomTo(scale / 1.5, null);
    });

    node.addEventListener('wheel', (event) => {
      event.preventDefault();
      const factor = Math.exp(-event.deltaY * 0.0015);
      zoomTo(scale * factor, stageOrigin(event.clientX, event.clientY), false);
    }, { passive: false });

    document.addEventListener('keydown', (event) => {
      if (!overlay || overlay.hidden) return;

      switch (event.key) {
        case 'Escape': event.preventDefault(); close(); break;
        case 'ArrowLeft': event.preventDefault(); step(-1); break;
        case 'ArrowRight': event.preventDefault(); step(1); break;
        case '+': case '=': event.preventDefault(); zoomTo(scale * 1.5, null); break;
        case '-': event.preventDefault(); zoomTo(scale / 1.5, null); break;
        case '0': event.preventDefault(); resetTransform(); break;
        case 'Tab': trapFocus(event); break;
        default: break;
      }
    });

    bindPointer(node);
  };

  // 打开着的时候焦点不许跑到后面的页面上去
  const trapFocus = (event) => {
    const focusable = Array.from(overlay.querySelectorAll('button:not([hidden])'));
    if (!focusable.length) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

    /*
     * 使用 Pointer Events 统一处理鼠标和触摸交互：
     * - 单指：缩放状态下拖动画面，否则执行翻页/下滑关闭。
     * - 双指：执行捏合缩放。
     * - 点击：画布区域关闭，双击切换缩放状态。
     *
     * 不依赖 click 的 target 判断点击区域。
     * Pointer Capture 会导致 click 事件目标被重新分派，
     * 因此在 pointerdown 阶段记录初始命中元素，避免误判图片点击。
     */
  const bindPointer = (node) => {
    const active = new Map();
    let startX = 0;
    let startY = 0;
    let baseX = 0;
    let baseY = 0;
    let baseScale = 1;
    let baseSpan = 0;
    let dragging = false;
    let swiping = false;
    let onCanvas = false;
    let lastTap = 0;

    const span = () => {
      const points = Array.from(active.values());
      return Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y);
    };

    const centre = () => {
      const points = Array.from(active.values());
      return {
        x: (points[0].x + points[1].x) / 2,
        y: (points[0].y + points[1].y) / 2
      };
    };

    const settleSwipe = (dx, dy) => {
      node.classList.remove('is-swiping');
      node.style.removeProperty('--lightbox-dim');

      if (Math.abs(dx) > 70 && Math.abs(dx) > Math.abs(dy)) {
        step(dx < 0 ? 1 : -1);
        return;
      }

      if (dy > 110) {
        close();
        return;
      }

      applyTransform(true);
    };

    // 没挪动过的那一下：点空白关掉，连着点画布两下切换放大
    const settleTap = (clientX, clientY) => {
      if (!onCanvas) {
        close();
        return;
      }

      const now = Date.now();
      if (now - lastTap < 320) {
        lastTap = 0;
        zoomTo(scale > 1.001 ? MIN_SCALE : 2.5, stageOrigin(clientX, clientY));
      } else {
        lastTap = now;
      }
    };

    node.addEventListener('pointerdown', (event) => {
      // 按钮那几个交给 click，别把指针捕获走
      if (event.target.closest('[data-act]')) return;

      active.set(event.pointerId, { x: event.clientX, y: event.clientY });
      if (node.setPointerCapture) node.setPointerCapture(event.pointerId);

      if (active.size === 2) {
        baseSpan = span();
        baseScale = scale;
        dragging = false;
        swiping = false;
        return;
      }

      startX = event.clientX;
      startY = event.clientY;
      baseX = offsetX;
      baseY = offsetY;
      onCanvas = event.target.closest('[data-canvas]') !== null;
      dragging = onCanvas && scale > 1.001;
      swiping = onCanvas && !dragging;
      if (dragging) node.classList.add('is-dragging');
    });

    node.addEventListener('pointermove', (event) => {
      if (!active.has(event.pointerId)) return;
      active.set(event.pointerId, { x: event.clientX, y: event.clientY });

      if (active.size >= 2) {
        if (baseSpan > 0) zoomTo(baseScale * (span() / baseSpan), stageOrigin(centre().x, centre().y), false);
        return;
      }

      const dx = event.clientX - startX;
      const dy = event.clientY - startY;

      if (dragging) {
        offsetX = baseX + dx;
        offsetY = baseY + dy;
        applyTransform(false);
        return;
      }

      if (!swiping || (Math.abs(dx) < 6 && Math.abs(dy) < 6)) return;

      // 跟手：横向跟着走，往下拖时顺带淡出，松手再决定是翻页还是关闭
      node.classList.add('is-swiping');
      parts.canvas.style.transition = 'none';
      parts.canvas.style.transform = 'translate(' + dx + 'px,' + Math.max(0, dy) + 'px) scale(1)';
      node.style.setProperty('--lightbox-dim', String(Math.max(0.35, 1 - (Math.max(0, dy) / 420))));
    });

    const release = (event) => {
      if (!active.has(event.pointerId)) return;

      const dx = event.clientX - startX;
      const dy = event.clientY - startY;
      const moved = Math.abs(dx) > 6 || Math.abs(dy) > 6;
      active.delete(event.pointerId);

      node.classList.remove('is-dragging');

      if (active.size >= 1) {
        // 松开一根手指后，剩下那根重新算起点，别让图猛地跳一下
        const rest = Array.from(active.values())[0];
        startX = rest.x;
        startY = rest.y;
        baseX = offsetX;
        baseY = offsetY;
        dragging = scale > 1.001;
        swiping = false;
        return;
      }

      if (!moved && event.type === 'pointerup') settleTap(event.clientX, event.clientY);
      else if (swiping) settleSwipe(dx, dy);
      else applyTransform(false);

      dragging = false;
      swiping = false;
      baseSpan = 0;
    };

    node.addEventListener('pointerup', release);
    node.addEventListener('pointercancel', release);
  };

  /* ---- 页面里的触发元素 ---- */

  const read = (trigger) => {
    const image = trigger.querySelector('img');
    return {
      full: trigger.dataset.full || (image ? image.currentSrc || image.src : ''),
      preview: trigger.dataset.preview || (image ? image.currentSrc || image.src : ''),
      alt: trigger.dataset.alt || (image ? image.alt : ''),
      caption: trigger.dataset.caption || ''
    };
  };

  /**
   * 认领 root 里所有 data-lightbox 触发元素。
   * 动态插进来的内容再调一次就行，已经认领过的不会重复绑。
   */
  const scan = (root) => {
    const scope = root || document;
    scope.querySelectorAll('[data-lightbox]:not([data-lightbox-ready])').forEach((trigger) => {
      trigger.dataset.lightboxReady = '1';
      trigger.addEventListener('click', (event) => {
        event.preventDefault();

        const group = trigger.dataset.lightbox;
        const peers = Array.from(document.querySelectorAll('[data-lightbox="' + group.replace(/"/g, '\\"') + '"]'));
        const index = Math.max(0, peers.indexOf(trigger));

        open(peers.map(read), index);
      });
    });
  };

  window.ccLightbox = { open: open, close: close, scan: scan };

  scan(document);
}());
