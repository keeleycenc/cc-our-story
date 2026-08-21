document.querySelectorAll('[data-answer-form]').forEach(function (form) {
    var choices = Array.from(form.querySelectorAll('input[name="optionIndexes"]'));
    var textAnswer = form.querySelector('textarea[name="textAnswer"]');
    var fieldset = form.querySelector('fieldset');
    var error = form.querySelector('[data-answer-error]');

    function hasAnswer() {
        return choices.some(function (choice) { return choice.checked; })
            || Boolean(textAnswer && textAnswer.value.trim());
    }

    function clearError() {
        if (!hasAnswer()) return;
        fieldset && fieldset.classList.remove('has-error');
        textAnswer && textAnswer.classList.remove('has-error');
        if (error) error.hidden = true;
    }

    choices.forEach(function (choice) { choice.addEventListener('change', clearError); });
    textAnswer && textAnswer.addEventListener('input', clearError);
    form.addEventListener('submit', function (event) {
        if (hasAnswer()) return;
        event.preventDefault();
        fieldset && fieldset.classList.add('has-error');
        textAnswer && textAnswer.classList.add('has-error');
        if (error) error.hidden = false;
        if (textAnswer) textAnswer.focus();
        else choices[0] && choices[0].focus();
    });
});

document.querySelectorAll('.reveal-answers strong, .history-answer > p').forEach(function (answer, index) {
    answer.classList.add('answer-copy');

    var collapseHeight = parseFloat(getComputedStyle(answer).getPropertyValue('--answer-collapse-height'));
    if (!collapseHeight || answer.scrollHeight <= collapseHeight + 1) return;

    var answerId = answer.id || 'affinity-answer-' + index;
    answer.id = answerId;
    answer.classList.add('is-collapsible', 'is-collapsed');

    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'answer-expand';
    button.setAttribute('aria-controls', answerId);
    button.setAttribute('aria-expanded', 'false');
    button.textContent = '展开全部 ↓';
    answer.insertAdjacentElement('afterend', button);

    button.addEventListener('click', function () {
        var collapsed = answer.classList.toggle('is-collapsed');
        button.setAttribute('aria-expanded', String(!collapsed));
        button.textContent = collapsed ? '展开全部 ↓' : '收起 ↑';
    });
});

function targetHistoryCard() {
    if (!window.location.hash) return null;

    var targetId;
    try {
        targetId = decodeURIComponent(window.location.hash.slice(1));
    } catch (_) {
        return null;
    }

    var target = document.getElementById(targetId);
    return target && target.classList.contains('history-card') ? target : null;
}

function positionTargetHistoryCard() {
    document.querySelectorAll('.history-card.is-target-highlight').forEach(function (card) {
        card.classList.remove('is-target-highlight');
    });

    var target = targetHistoryCard();
    if (!target) return;

    function startHighlight() {
        target.classList.add('is-target-highlight');
        target.addEventListener('animationend', function finishHighlight(event) {
            if (event.target !== target) return;
            target.classList.remove('is-target-highlight');
            target.removeEventListener('animationend', finishHighlight);
        });
    }

    var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var expectedTop = parseFloat(window.getComputedStyle(target).scrollMarginTop) || 0;
    var startedAt = window.performance.now();

    target.scrollIntoView({
        behavior: reduceMotion ? 'auto' : 'smooth',
        block: 'start'
    });

    function waitForPosition() {
        var top = target.getBoundingClientRect().top;
        var reachedTarget = Math.abs(top - expectedTop) <= 2;
        var stoppedWaiting = window.performance.now() - startedAt >= 2000;
        if (reachedTarget || stoppedWaiting) {
            startHighlight();
            return;
        }

        window.requestAnimationFrame(waitForPosition);
    }

    window.requestAnimationFrame(waitForPosition);
}

function scheduleTargetHistoryPosition() {
    var fontsReady = document.fonts ? document.fonts.ready : Promise.resolve();
    fontsReady.then(function () {
        window.requestAnimationFrame(function () {
            window.requestAnimationFrame(positionTargetHistoryCard);
        });
    });
}

scheduleTargetHistoryPosition();
window.addEventListener('hashchange', scheduleTargetHistoryPosition);
window.addEventListener('pageshow', function (event) {
    if (event.persisted) scheduleTargetHistoryPosition();
});
