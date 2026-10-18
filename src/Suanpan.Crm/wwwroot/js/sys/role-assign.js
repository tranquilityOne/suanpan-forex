// 角色授权的权限树（sys 设计 §9.1）：勾选或取消目录、菜单时，其下全部节点跟着变；勾选按钮时自动勾选它的菜单和目录
(function () {
    'use strict';

    const boxes = Array.from(document.querySelectorAll('input[data-perm-id]'));
    const byId = new Map(boxes.map(function (box) { return [box.dataset.permId, box]; }));

    function setDescendants(box, checked) {
        boxes
            .filter(function (child) { return child.dataset.parentId === box.dataset.permId; })
            .forEach(function (child) {
                child.checked = checked;
                setDescendants(child, checked);
            });
    }

    function checkAncestors(box) {
        let parent = byId.get(box.dataset.parentId);
        while (parent) {
            parent.checked = true;
            parent = byId.get(parent.dataset.parentId);
        }
    }

    boxes.forEach(function (box) {
        box.addEventListener('change', function () {
            setDescendants(box, box.checked);
            if (box.checked) {
                checkAncestors(box);
            }
        });
    });

    document.querySelectorAll('[data-check-all]').forEach(function (button) {
        button.addEventListener('click', function () {
            const checked = button.dataset.checkAll === 'true';
            boxes.forEach(function (box) { box.checked = checked; });
        });
    });
})();
