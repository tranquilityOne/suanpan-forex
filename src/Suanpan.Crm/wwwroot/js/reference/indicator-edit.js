// 指标表单（基础数据设计 §6）：新增时选了经济体，货币默认设为经济体的货币；编辑时影响方向改变，"修改理由"标为必填
(function () {
    'use strict';

    const form = document.getElementById('indicator-form');
    if (!form) {
        return;
    }

    const economy = form.querySelector('select[data-economy-currencies]');
    const currency = form.querySelector('select[name="CurrencyCode"]');
    if (economy && currency) {
        const currencies = JSON.parse(economy.dataset.economyCurrencies || '{}');
        economy.addEventListener('change', function () {
            const code = currencies[economy.value];
            if (code && currency.querySelector('option[value="' + code + '"]')) {
                currency.value = code;
            }
        });
    }

    const direction = form.querySelector('select[name="Direction"]');
    const reasonLabel = document.getElementById('reason-label');
    if (direction && reasonLabel && direction.dataset.original) {
        const toggle = function () {
            reasonLabel.classList.toggle('required', direction.value !== direction.dataset.original);
        };
        direction.addEventListener('change', toggle);
        toggle();
    }
})();
