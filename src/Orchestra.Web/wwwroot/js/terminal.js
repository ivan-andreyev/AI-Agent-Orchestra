/**
 * Terminal interaction JavaScript functions for AgentTerminalComponent
 * Provides interop for scrolling, history persistence, and clipboard operations
 */

window.terminalFunctions = {
    /**
     * Scrolls terminal output element to bottom
     * @param {HTMLElement} element - Terminal output element
     */
    scrollToBottom: (element) => {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },

    /**
     * Saves command history to localStorage
     * @param {string[]} history - Array of command strings
     */
    saveHistory: (history) => {
        try {
            localStorage.setItem('terminal-history', JSON.stringify(history));
        } catch (e) {
            console.warn('Failed to save terminal history:', e);
        }
    },

    /**
     * Loads command history from localStorage
     * @returns {string[]} Array of command strings
     */
    loadHistory: () => {
        try {
            const history = localStorage.getItem('terminal-history');
            return history ? JSON.parse(history) : [];
        } catch (e) {
            console.warn('Failed to load terminal history:', e);
            return [];
        }
    },

    /**
     * Copies text to clipboard
     * @param {string} text - Text to copy
     * @returns {Promise<boolean>} True if successful
     */
    copyToClipboard: async (text) => {
        try {
            await navigator.clipboard.writeText(text);
            console.log('Copied to clipboard');
            return true;
        } catch (e) {
            console.error('Failed to copy to clipboard:', e);
            return false;
        }
    },

    /**
     * Selects all text content of an element
     * @param {HTMLElement} element - Element to select
     */
    selectLine: (element) => {
        try {
            const selection = window.getSelection();
            const range = document.createRange();
            range.selectNodeContents(element);
            selection.removeAllRanges();
            selection.addRange(range);
        } catch (e) {
            console.error('Failed to select line:', e);
        }
    },

    /**
     * Focuses the terminal input element
     * @param {string} selector - CSS selector for input element
     */
    focusInput: (selector) => {
        try {
            const input = document.querySelector(selector);
            if (input) {
                input.focus();
            }
        } catch (e) {
            console.error('Failed to focus input:', e);
        }
    }
};
