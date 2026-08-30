/* 花信如期后台：模型测试与立即补写使用就地请求。

   模型调用可能持续数秒。独立请求只锁定当前操作区域，不触发页面导航，
   因此不会丢失页面状态，也不会阻塞其他后台设置。 */
(() => {
    'use strict';

    const host = document.querySelector('[data-cycle-insight-actions]');
    if (!host) return;

    const buttons = Array.from(host.querySelectorAll('[data-cycle-insight-action]'));
    const feedback = host.querySelector('[data-cycle-insight-feedback]');
    const message = host.querySelector('[data-cycle-insight-message]');
    const text = host.querySelector('[data-cycle-insight-text]');

    const busy = (active, on) => {
        buttons.forEach((button) => {
            button.disabled = on;
            button.classList.toggle('is-busy', on && button === active);
        });
    };

    const report = (result) => {
        const ok = Boolean(result && result.ok);
        feedback.hidden = false;
        feedback.classList.toggle('is-ok', ok);
        feedback.classList.toggle('is-bad', !ok);
        message.textContent = result && result.message
            ? result.message
            : (ok ? '操作已完成。' : '暂时未收到有效响应，请稍后重试。');

        const detail = result && result.text ? result.text : '';
        text.textContent = detail;
        text.hidden = !detail;
    };

    const execute = async (button) => {
        const action = button.dataset.cycleInsightAction;
        const endpoint = action === 'refresh' ? host.dataset.refreshUrl : host.dataset.testUrl;

        busy(button, true);
        feedback.hidden = false;
        feedback.classList.remove('is-ok', 'is-bad');
        message.textContent = action === 'refresh' ? '正在补写花信小结……' : '正在测试模型通道……';
        text.hidden = true;
        text.textContent = '';

        try {
            const response = await fetch(endpoint, {
                method: 'POST',
                headers: { Accept: 'application/json' },
                credentials: 'same-origin'
            });
            const result = await response.json().catch(() => null);
            report(result || {
                ok: false,
                message: '暂时无法读取服务端响应，请稍后重试。详细原因请查看站点日志。'
            });
        } catch {
            report({
                ok: false,
                message: '暂时无法连接到站点服务，请检查网络或稍后重试。'
            });
        } finally {
            busy(button, false);
        }
    };

    buttons.forEach((button) => {
        button.addEventListener('click', () => execute(button));
    });
})();
