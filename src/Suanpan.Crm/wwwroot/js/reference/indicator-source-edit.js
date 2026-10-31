// 指标数据源映射的表单（基础数据设计 §6.2）：新增时用途选"排期"，优先级和盯盘参数不填，输入框停用；
// 改回"公布值"时恢复，优先级空着时默认为 1（主源）
(function () {
    'use strict';

    const form = document.getElementById('indicator-source-form');
    if (!form) {
        return;
    }

    const purpose = form.querySelector('select[name="Purpose"]');
    const priority = form.querySelector('input[name="Priority"]');
    const priorityLabel = document.getElementById('priority-label');
    if (!purpose || !priority || !priorityLabel) {
        return;
    }

    const watchInputs = form.querySelectorAll('[data-release-only] input');

    const toggle = function () {
        const isSchedule = purpose.value === purpose.dataset.scheduleValue;
        priority.disabled = isSchedule;
        priorityLabel.classList.toggle('required', !isSchedule);
        if (isSchedule) {
            priority.value = '';
        } else if (priority.value === '') {
            priority.value = '1';
        }

        watchInputs.forEach(function (input) {
            input.disabled = isSchedule;
            if (isSchedule) {
                input.value = '';
            }
        });
    };
    purpose.addEventListener('change', toggle);
})();
