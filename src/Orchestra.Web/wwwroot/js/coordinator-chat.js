// Auto-scroll functionality for coordinator chat
window.coordinatorChat = {
    scrollToBottom: function(elementSelector) {
        try {
            const element = document.querySelector(elementSelector);
            if (element) {
                // Scroll to the last child element
                const lastMessage = element.lastElementChild;
                if (lastMessage) {
                    lastMessage.scrollIntoView({ behavior: 'smooth', block: 'end' });
                }
            }
        } catch (error) {
            console.error('Error scrolling chat:', error);
        }
    }
};
