<template>
    <div
        v-on:click="toggleSelection"
        v-show="isEnabled"
        class="selectable-box"
        :class="{'selected-item': isItemSelected}"
    ></div>
</template>

<script>
    export default {
        name: "GalleryItemSelect",
        props: {
            itemId: {
                type: String,
                required: true,
            },
            isEnabled: {
                type: Boolean,
                default: false,
            },
            isItemSelected: {
                type: Boolean,
                default: false,
            }
        },
        methods: {
            toggleSelection() {
                const state = {
                    itemId: this.itemId,
                    active: this.isItemSelected,
                };
                this.$emit('update:selection-event', state);
            },
        }
    }
</script>

<style scoped>
    .selected-item {
        border: 0.5rem solid #1099ff !important;
    }

    .selectable-box {
        background: transparent;
        position: absolute;
        z-index: 9997;
        width: 100%;
        height: 100%;
        top: 0;
        left: 0;
        border-radius: 10px;
        border: dashed gray;
    }
</style>
