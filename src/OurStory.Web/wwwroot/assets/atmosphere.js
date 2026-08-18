/* 氛围组后台：将「试一句 / 立即留言」改为就地请求。

   模型调用通常需要几秒到几十秒。若使用整页提交，请求期间浏览器会持续处于加载状态，
   页面中的其他操作也会受到影响。改为接口请求后，仅当前操作按钮显示忙碌状态，
   页面其余功能仍可正常使用。

   在脚本不可用时，表单仍会回退为传统整页提交，以确保基础功能可用。 */
(() => {
    'use strict';

    const ENDPOINT = '/api/atmosphere/test';

    /** 切换表单的忙碌状态，仅当前提交按钮显示加载效果。 */
    const busy = (form, on) => {
        form.querySelectorAll('button[type="submit"]').forEach((button) => {
            button.disabled = on;

            // 仅让实际触发请求的按钮显示加载状态，其余提交按钮暂时禁用
            button.classList.toggle('is-busy', on && button === form.pressed);
        });

        form.querySelectorAll('select').forEach((select) => {
            select.disabled = on;
        });
    };

    /** 在当前角色卡片下方显示本次试聊结果。 */
    const report = (item, probe) => {
        let box = item.querySelector('.atmosphere-probe');

        if (!box) {
            box = document.createElement('div');
            box.className = 'atmosphere-probe';
            item.appendChild(box);
        }

        box.classList.toggle('is-ok', probe.ok);
        box.classList.toggle('is-bad', !probe.ok);
        box.textContent = '';

        const head = document.createElement('p');
        head.className = 'atmosphere-probe-head';
        head.textContent = probe.message || (probe.ok ? '已经准备好啦。' : '这次没有成功。');
        box.appendChild(head);

        // 模型返回内容始终以纯文本插入，避免被浏览器解析为 HTML
        if (probe.ok && probe.text) {
            const quote = document.createElement('blockquote');
            quote.textContent = probe.text;
            box.appendChild(quote);
        }

        // 留言保存成功后同步更新当前计数，无需刷新页面
        if (probe.saved) {
            const tally = item.querySelector('[data-tally]');

            if (tally) {
                const count = Number(tally.dataset.tally || 0) + 1;
                tally.textContent = `${count} 条`;
                tally.dataset.tally = count;
            }
        }
    };

    document.querySelectorAll('form.atmosphere-try').forEach((form) => {
        const item = form.closest('.atmosphere-item');
        if (!item) {
            return;
        }

        // 部分浏览器可能无法提供 submitter，因此额外记录最后点击的提交按钮
        form.addEventListener('click', (event) => {
            const button = event.target.closest('button[type="submit"]');

            if (button) {
                form.pressed = button;
            }
        });

        form.addEventListener('submit', async (event) => {
            const button = event.submitter || form.pressed;

            // 无法确定提交来源时保留浏览器默认表单行为
            if (!button) {
                return;
            }

            event.preventDefault();

            form.pressed = button;
            busy(form, true);

            const payload = {
                memberId: form.querySelector('input[name="id"]').value,
                topicId: Number(form.querySelector('select[name="topicId"]').value || 0),
                persist: button.value === 'true'
            };

            try {
                const response = await fetch(ENDPOINT, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    credentials: 'same-origin',
                    body: JSON.stringify(payload)
                });

                const probe = await response.json().catch(() => null);

                report(item, probe || {
                    ok: false,
                    message: '暂时无法读取服务端的回复，可以稍后再试。详细原因请查看站点日志。'
                });
            } catch {
                report(item, {
                    ok: false,
                    message: '暂时无法连接到站点服务，请检查网络或稍后再试。'
                });
            } finally {
                busy(form, false);
                form.pressed = null;
            }
        });
    });
})();