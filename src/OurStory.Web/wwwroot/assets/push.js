/**
 * 后台「通知」页按钮交互逻辑
 * 
 * 核心约束（浏览器安全策略）：
 * 1. 通知权限请求（requestPermission）必须由用户点击事件同步触发
 *    禁止在点击回调中先 await 网络请求，否则浏览器将视为自动弹窗并拦截
 * 
 * 2. 订阅状态查询（getSubscription）耗时不可控，须采用异步非阻塞方案：
 *    - Chrome：依赖 Google 推送服务，网络异常时可阻塞长达 1-2 分钟
 *    - Edge：基于 Windows 原生通知服务，通常数百毫秒内返回
 * 
 * 实现策略：
 * - 优先使用本地缓存的订阅状态完成按钮渲染（避免页面白屏）
 * - 后台异步核实真实订阅状态，每步操作均设置超时保护
 * - 核实未完成时，按钮仍保持可点击，不影响用户交互
 */
(function () {
  'use strict';

  const panel = document.querySelector('[data-push]');
  if (!panel) return;

  const status = panel.querySelector('[data-push-status]');
  const toggle = panel.querySelector('[data-push-toggle]');
  const label = panel.querySelector('[data-push-label]');
  const spinner = panel.querySelector('[data-push-busy]');
  const sendForm = document.querySelector('[data-push-send]');

  const MEMORY = 'cc-push-endpoint';
  const remember = (value) => {
    try {
      if (value) localStorage.setItem(MEMORY, value);
      else localStorage.removeItem(MEMORY);
    } catch (e) {}
  };
  const remembered = () => {
    try { return localStorage.getItem(MEMORY); } catch (e) { return null; }
  };

    /**
    * 设备固定编号（设备唯一标识）
    *
    * 说明：
    * 推送端点（endpoint）具有不稳定性——用户在浏览器中撤销后重新授予通知权限，
    * 系统将生成全新的推送地址。若后台仅依赖该地址识别设备，则同一设备每次
    * 重新授权都会在设备列表中产生冗余记录，导致重复推送。
    *
    * 解决方案：
    * 本地生成并持久化存储设备固定编号，该编号不受权限撤销、账号切换等操作影响。
    * 后台以此编号作为设备唯一凭证，可准确识别并管理设备，避免重复注册与推送。
    */
  const DEVICE_KEY = 'cc-push-device';
  const deviceKey = () => {
    try {
      let key = localStorage.getItem(DEVICE_KEY);
      if (!key) {
        key = (crypto.randomUUID ? crypto.randomUUID() : String(Date.now()) + Math.random()).replace(/-/g, '');
        localStorage.setItem(DEVICE_KEY, key);
      }
      return key;
    } catch (e) {
      return '';
    }
  };

  const markCurrentDevice = () => {
    const key = (() => {
      try { return localStorage.getItem(DEVICE_KEY); } catch (e) { return null; }
    })();

    if (!key) return;

    const card = document.querySelector('[data-device-key="' + key + '"]');
    if (!card) return;

    card.classList.add('is-current');
    const badge = card.querySelector('.device-current');
    if (badge) badge.hidden = false;
  };

  const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  const standalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
  const isApple = /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

  const say = (text, kind) => {
    if (!status) return;
    status.textContent = text;
    status.dataset.kind = kind || '';
  };

  const LABELS = { busy: '请稍候', off: '未开启', on: '已开启' };
  let state = 'busy';

  const render = (next, busyLabel) => {
    state = next;

    // 忙的时候转圈挂在标题旁边，开关本身保持原样，不会闪
    if (spinner) spinner.hidden = next !== 'busy';
    if (!toggle) return;

    toggle.dataset.state = next;
    if (label) label.textContent = next === 'busy' ? (busyLabel || LABELS.busy) : LABELS[next];

    toggle.setAttribute('aria-checked', String(next === 'on'));
    toggle.disabled = next === 'busy';
    toggle.setAttribute('title', next === 'on' ? '点一下关闭这台设备的通知' : '点一下在这台设备上开启通知');
  };

  const busy = (button, on) => {
    if (!button) return;
    button.disabled = on;
    button.classList.toggle('is-busy', on);
  };

  const TIMED_OUT = Symbol('timed-out');
  const within = (promise, ms) => Promise.race([
    promise,
    new Promise((resolve) => setTimeout(() => resolve(TIMED_OUT), ms))
  ]);

  /* VAPID 公钥在页面上是 base64url 的字符串，subscribe 要的是字节数组 */
  const toBytes = (value) => {
    const padded = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/');
    const raw = window.atob(padded);
    const bytes = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
    return bytes;
  };

  const post = async (url, body) => {
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify(body || {})
    });

    return response.json().catch(() => ({ ok: false, message: '暂时未收到服务器响应，请稍后再试。' }));
  };

  if (!supported) {
    say(isApple && !standalone
      ? '在 iPhone 或 iPad 上，请先在 Safari 中选择「分享 → 添加到主屏幕」，再从主屏幕图标打开本站，才能开启通知'
      : '当前浏览器暂不支持网页通知，请使用 Chrome、Edge、Firefox 或 Safari 访问。', 'warn');
    render('off');
    if (toggle) toggle.disabled = true;
    return;
  }

  if (window.isSecureContext === false) {
    say('内网环境下，浏览器通知功能仅支持 HTTPS 或 localhost 协议。'
        + 'Android 设备可通过 USB 连接电脑，在 Chrome 的 chrome://inspect 中配置端口转发，'
        + '使手机通过 localhost 访问本地服务；iOS 设备或希望简化操作时，'
        + '可使用 cloudflared 等工具创建临时 HTTPS 隧道，获取可用地址。', 'warn');
    render('off');
    if (toggle) toggle.disabled = true;
    return;
  }

  const install = () => navigator.serviceWorker.register('/sw.js', { scope: '/' })
    .then(() => navigator.serviceWorker.ready);

  const currentSubscription = async () => {
    const registration = await navigator.serviceWorker.getRegistration('/');
    return registration ? registration.pushManager.getSubscription() : null;
  };

    const SLOW = '当前设备通知状态读取超时（Chrome 依赖 Google 推送服务，网络不畅时可能长时间阻塞）';

  const refresh = async () => {
    if (Notification.permission === 'denied') {
      say('当前设备的通知权限已被拒绝。如需重新启用，请在浏览器站点设置中将「通知」权限修改为「允许」。', 'warn');
      render('off');
      if (toggle) toggle.disabled = true;
      return;
    }

    render(remembered() ? 'on' : 'off');

    const subscription = await within(currentSubscription().catch(() => null), 6000);
    if (subscription === TIMED_OUT) {
      say(SLOW, 'warn');
      return;
    }

    if (!subscription) {
      remember(null);
      render('off');
      say('当前设备尚未开启通知，开启后即可接收彼此的动态', '');
      return;
    }

    remember(subscription.endpoint);

    const owned = await within(post('/api/push/state', { endpoint: subscription.endpoint }), 6000);
    if (owned === TIMED_OUT) {
      say(SLOW, 'warn');
      return;
    }

    if (owned.state === 'mine') {
      render('on');
      say('当前设备已开启通知，彼此的动态会及时送达', 'ok');
      return;
    }

    render('off');

    if (owned.state === 'other') {
      say('这台设备上的通知目前登记在另一个账号名下 —— 同一个浏览器只能有一份订阅', 'warn');
      return;
    }

      /* unknown：浏览器还留着订阅，服务端却查无此条。
             常见于开启到一半刷新了页面（订阅建好了，但没来得及登记），
             或者服务端记录被清过。
      
             这种订阅谁都没认领，而权限本来就已经给过了，直接补登记到当前账号
             就是用户想要的结果 —— 不必再让人多点一次 */
    const claimed = await within(claim(subscription), 8000);

    if (claimed !== TIMED_OUT && claimed && claimed.ok) {
      remember(subscription.endpoint);
      render('on');
      say('已检测到设备历史订阅，现已自动重新登记完成', 'ok');
      window.location.reload();
      return;
    }

      say('检测到本地订阅与服务器数据不同步，请点击开关重新登记以恢复通知功能', '');
  };

  /* 把手上这份订阅登记到当前账号名下 */
  const claim = async (subscription) => {
    const payload = subscription.toJSON();

    return post('/api/push/subscribe', {
      endpoint: subscription.endpoint,
      p256dh: payload.keys.p256dh,
      auth: payload.keys.auth,
      deviceKey: deviceKey()
    });
  };

  const enable = async () => {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') {
      say(permission === 'denied'
        ? '当前设备未授予通知权限。如需开启，请前往浏览器的站点设置进行调整。'
        : '尚未获得通知授权，请再次点击并确认允许。', 'warn');
      render('off');
      return;
    }

    render('busy', '正在开启');
    say('正在开启，Chrome 第一次可能要等一会儿……', '');

    const keyResponse = await fetch('/api/push/key', { credentials: 'same-origin' });
    const key = await keyResponse.json();
    if (!key.ok || !key.key) {
      say('站点还没有生成通知密钥，请检查启动日志。', 'warn');
      render('off');
      return;
    }

    const registration = await install();
    const subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: toBytes(key.key)
    });

    const result = await claim(subscription);

    if (!result.ok) {
      await subscription.unsubscribe().catch(() => {});
      remember(null);
      render('off');
      say(result.message || '这台设备登记失败，请稍后重试。', 'warn');
      return;
    }

    remember(subscription.endpoint);
    render('on');
    say('通知已开启，今后不会错过彼此的动态。', 'ok');
    window.location.reload();
  };

  const disable = async () => {
    render('busy', '正在关闭');
    say('正在关闭……', '');

    const known = remembered();
    const subscription = await within(currentSubscription().catch(() => null), 6000);
    remember(null);

    const endpoint = subscription && subscription !== TIMED_OUT ? subscription.endpoint : known;
    if (endpoint) {
      await post('/api/push/unsubscribe', { endpoint: endpoint });
    }

    if (subscription && subscription !== TIMED_OUT) {
      await subscription.unsubscribe().catch(() => {});
    }

    window.location.reload();
  };

  if (toggle) {
    toggle.addEventListener('click', async () => {
      const wasOn = state === 'on';
      render('busy', wasOn ? '正在关闭' : '正在开启');

      try {
        await (wasOn ? disable() : enable());
      } catch (error) {
        render(wasOn ? 'on' : 'off');
        say((wasOn ? '关闭' : '开启') + '通知时遇到问题：' + (error && error.message ? error.message : error), 'warn');
      }
    });
  }

  document.querySelectorAll('[data-device-test]').forEach((button) => {
    const card = button.closest('.device-card');
    const note = card ? card.querySelector('[data-device-note]') : null;

    button.addEventListener('click', async () => {
      busy(button, true);
      if (note) {
        note.textContent = '正在发送……';
        note.dataset.kind = '';
        note.hidden = false;
      }

      try {
        const result = await post('/api/push/test', { deviceId: Number(button.dataset.deviceTest) });
        if (note) {
            note.textContent = result.message || (result.ok ? '发送成功。' : '发送失败。');
          note.dataset.kind = result.ok ? 'ok' : 'warn';
        }
      } finally {
        busy(button, false);
      }
    });
  });

  if (sendForm) {
    const input = sendForm.querySelector('[data-push-body]');
    const feedback = sendForm.querySelector('[data-push-feedback]');
    const button = sendForm.querySelector('button');

    sendForm.addEventListener('submit', async (event) => {
      event.preventDefault();

      const body = (input.value || '').trim();
      if (!body) {
        input.focus();
        return;
      }

      busy(button, true);

      try {
        const result = await post('/api/push/send', { body: body });
        if (feedback) {
          feedback.textContent = result.message || (result.ok ? '心意已送达。' : '心意暂未送达，请稍后再试。');
          feedback.dataset.kind = result.ok ? 'ok' : 'warn';
        }

        if (result.ok) input.value = '';
      } finally {
        busy(button, false);
      }
    });
  }

    /**
     * 总开关关闭后，子选项应置灰且不可操作。
     * 
     * 使用 inert 而非 disabled：
     * - disabled 会阻止控件随表单提交，导致保存时子选项值被重置为默认值
     * - inert 仅屏蔽点击和聚焦，保留原有值正常提交，重新开启时状态恢复
     */
  const master = document.querySelector('[data-notify-master]');
  const group = document.querySelector('[data-notify-group]');
  if (master && group) {
    const sync = () => {
      const off = !master.checked;
      group.classList.toggle('is-off', off);
      group.inert = off;
    };

    master.addEventListener('change', sync);
    sync();
  }

  markCurrentDevice();

  refresh().catch(() => {
    render(remembered() ? 'on' : 'off');
    say('当前设备通知状态暂时无法读取，按钮仍可正常点击操作。', 'warn');
  });
}());
