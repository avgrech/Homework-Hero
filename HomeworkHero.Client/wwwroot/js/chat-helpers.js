window.chatHelpers = {
    scrollToBottom: (element) => {
        if (!element) {
            return;
        }

        element.scrollTop = element.scrollHeight;
    }
};
