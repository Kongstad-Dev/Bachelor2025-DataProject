export function registerClickOutside(element, dotnetHelper) {
    
    // Handler function for document click events
    const handleDocumentClick = (event) => {
        // Find the search container (parent of the input element)
        const searchContainer = element.closest('.search-container');
        
        // Check if click is outside search container (which includes dropdown)
        if (searchContainer && !searchContainer.contains(event.target)) {
            dotnetHelper.invokeMethodAsync('CloseDropdown');
        }
    };
    
    // Add click handler to document
    document.addEventListener('click', handleDocumentClick);

    // Return an object with a dispose method
    return {
        dispose: () => {
            document.removeEventListener('click', handleDocumentClick);
        }
    };
}

// Add this missing function
export function focusElement(element) {
    if (element) {
        element.focus();
    }
}

export function blurElement(element) {
    if (element) {
        element.blur();
    }
}