mergeInto(LibraryManager.library, {
    // 修复后的PostViewInfo方法，使用postMessage发送消息到父窗口
    PostViewInfo: function (jsonStrPtr) {
        try {
            // 将 C# 字符串转换为 JS 字符串
            const jsonStr = Pointer_stringify(jsonStrPtr);
            
            // 解析 JSON
            const viewInfo = JSON.parse(jsonStr);
            
            console.log("Unity 视角切换数据:", viewInfo);
            
            // 检查是否在iframe中
            if (window.parent && window.parent !== window) {
                // 使用postMessage发送消息到父窗口（Vue应用）
                window.parent.postMessage({
                    type: 'unityViewChanged',
                    data: viewInfo
                }, 'http://localhost:9528'); // 实际应用中应该指定具体的域名，而不是 '*'
                
                console.log("Unity 已发送消息到Vue:", viewInfo);
            } else {
                // 如果不是在iframe中，使用自定义事件（用于开发测试）
                const event = new CustomEvent('unityViewChanged', {
                    detail: viewInfo
                });
                window.dispatchEvent(event);
                
                console.log("Unity 已发送自定义事件:", viewInfo);
            }
            
        } catch (error) {
            console.error("发送视角切换消息时出错:", error, "原始数据:", jsonStrPtr);
        }
    }
});