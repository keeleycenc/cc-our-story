/* 花信如期后台：模型通道测试使用就地请求。

   模型调用可能持续数秒。独立请求只锁定当前操作区域，不触发页面导航，
   因此不会丢失页面状态，也不会阻塞其他后台设置。
   小结的正式补写由后台任务执行，这里只做一次不落库的试写。 */
(() => {
    'use strict';

    const host = document.querySelector('[data-cycle-insight-actions]');
    if (!host) return;

    const button = host.querySelector('[data-cycle-insight-action="test"]');
    const feedback = host.querySelector('[data-cycle-insight-feedback]');
    const message = host.querySelector('[data-cycle-insight-message]');
    const text = host.querySelector('[data-cycle-insight-text]');
    if (!button) return;

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

    const execute = async () => {
        button.disabled = true;
        button.classList.add('is-busy');
        feedback.hidden = false;
        feedback.classList.remove('is-ok', 'is-bad');
        message.textContent = '正在测试模型通道……';
        text.hidden = true;
        text.textContent = '';

        try {
            const response = await fetch(host.dataset.testUrl, {
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
            button.disabled = false;
            button.classList.remove('is-busy');
        }
    };

    button.addEventListener('click', () => execute());
})();
