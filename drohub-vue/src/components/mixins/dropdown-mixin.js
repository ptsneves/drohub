export default {
    data() {
        return {
            areOptionsVisible: false,
        };
    },
    methods: {
        showOptions() {
            this.areOptionsVisible = true;
        },
        hideOptions(event) {
            this.areOptionsVisible = false;
        },
    }
}
