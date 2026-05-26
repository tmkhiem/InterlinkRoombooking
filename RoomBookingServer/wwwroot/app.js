window.roomBookingDropzone = {
    openPicker(event) {
        const zone = event.currentTarget;
        const input = this.resolveInput(zone);
        if (!input || input.disabled) {
            return;
        }

        input.click();
    },

    dragOver(event) {
        event.preventDefault();
    },

    dropFiles(event) {
        event.preventDefault();

        const zone = event.currentTarget;
        const input = this.resolveInput(zone);
        if (!input || input.disabled) {
            return;
        }

        const files = event.dataTransfer?.files;
        if (!files || files.length === 0) {
            return;
        }

        const transfer = new DataTransfer();
        for (const file of files) {
            transfer.items.add(file);
        }

        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    },

    resolveInput(zone) {
        const inputId = zone?.dataset?.inputId;
        if (!inputId) {
            return null;
        }

        return document.getElementById(inputId);
    }
};
