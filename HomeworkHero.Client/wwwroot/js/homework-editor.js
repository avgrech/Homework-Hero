window.homeworkEditor = {
    async init(element, initialValue, dotNetHelper) {
        if (!element || !window.ClassicEditor) {
            return;
        }

        await this.dispose(element);

        const editor = await ClassicEditor.create(element, {
            toolbar: {
                items: [
                    'heading', '|',
                    'bold', 'italic', 'underline', 'link', '|',
                    'bulletedList', 'numberedList', '|',
                    'blockQuote', 'insertTable', 'undo', 'redo'
                ]
            }
        });

        editor.setData(initialValue || '');
        editor.model.document.on('change:data', () => {
            dotNetHelper.invokeMethodAsync('OnEditorChanged', editor.getData());
        });

        element._homeworkEditorInstance = editor;
        element._homeworkDotNetRef = dotNetHelper;
    },

    updateData(element, value) {
        if (element?._homeworkEditorInstance) {
            element._homeworkEditorInstance.setData(value || '');
        }
    },

    async dispose(element) {
        if (element?._homeworkEditorInstance) {
            await element._homeworkEditorInstance.destroy();
            element._homeworkEditorInstance = null;
        }
    }
};
