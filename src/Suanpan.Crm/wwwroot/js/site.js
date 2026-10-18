// 后台公共脚本（前端约定 §4.2、§6）：二次确认弹窗、HTMX 出错提示、HTMX 插入内容后重新解析表单校验
(function () {
    'use strict';

    // 显示布局中的公共确认弹窗，点"确定"后执行 proceed
    function confirmThen(message, buttonText, proceed) {
        const modalElement = document.getElementById('confirm-modal');
        modalElement.querySelector('[data-confirm-message]').textContent = message;
        const ok = modalElement.querySelector('[data-confirm-ok]');
        ok.textContent = buttonText || '确定';

        const modal = tabler.Modal.getOrCreateInstance(modalElement);
        ok.onclick = function () {
            modal.hide();
            proceed();
        };
        modal.show();
    }

    // <confirm-form> 输出 form[data-confirm]：提交前先确认。HTMX 表单走下面的 htmx:confirm
    document.addEventListener('submit', function (event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.confirm
            || form.matches('[hx-post],[hx-put],[hx-patch],[hx-delete]')) {
            return;
        }

        event.preventDefault();
        confirmThen(form.dataset.confirm, form.dataset.confirmButton, function () {
            // 直接调用原生 submit，不再触发 submit 事件
            HTMLFormElement.prototype.submit.call(form);
        });
    });

    // HTMX 元素上的二次确认：带 data-confirm 的元素发请求前先确认
    document.addEventListener('htmx:confirm', function (event) {
        const element = event.detail.elt;
        const message = element.dataset ? element.dataset.confirm : null;
        if (!message) {
            return;
        }

        event.preventDefault();
        confirmThen(message, element.dataset.confirmButton, function () {
            event.detail.issueRequest(true);
        });
    });

    function showError(message) {
        const box = document.getElementById('page-error');
        box.querySelector('[data-error-message]').textContent = message;
        box.classList.remove('d-none');
        box.scrollIntoView({ block: 'nearest' });
    }

    // HTMX 请求出错：401（会话超时）已由服务端的 HX-Redirect 整页跳到登录页；403 提示无权限（sys 设计 §5.2）
    document.addEventListener('htmx:responseError', function (event) {
        const status = event.detail.xhr.status;
        if (status === 401) {
            return;
        }

        showError(status === 403 ? '无权限执行此操作' : '操作失败（HTTP ' + status + '），请稍后重试');
    });

    document.addEventListener('htmx:sendError', function () {
        showError('网络异常，请检查网络后重试');
    });

    // HTMX 插入的表单要重新解析校验规则，否则客户端校验不生效（unobtrusive 只在页面加载时解析一次）
    htmx.onLoad(function (element) {
        if (element === document.body || !window.jQuery || !jQuery.validator || !jQuery.validator.unobtrusive) {
            return;
        }

        jQuery.validator.unobtrusive.parse(element);
    });
})();
