<template>
    <div class="gallery-item-select"
         v-show="isEnabled"
    >
        <inline-svg
            v-bind:src="require('../../../../wwwroot/images/assets/timeline-select-selected.svg')"
            v-on:click="toggleSelection"
            v-show="isItemSelected"
            class="item-select"
            v-bind:transform-source="transform"
        ></inline-svg>
        <inline-svg
            v-bind:src="require('../../../../wwwroot/images/assets/timeline-select-unselected.svg')"
            v-on:click="toggleSelection"
            v-show="!isItemSelected"
            class="item-select"
            v-bind:transform-source="transform"
        ></inline-svg>
    </div>
</template>

<script>
    import InlineSvg from 'vue-inline-svg';
    export default {
        name: "GalleryItemSelect",
        components: {
            InlineSvg,
        },
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
            transform(svg) {
                let ids = svg.querySelectorAll('[id]');

                Array.prototype.forEach.call( ids, function( el, i ) {
                    let classes = el.id;
                    if (el.class !== undefined)
                        classes = el.class + " " + classes;
                    el.removeAttributeNS(null, 'id')
                    el.setAttributeNS(null, 'class', classes);
                });
                return svg;
            }
        }
    }
</script>

<style>
    .item-select {
        background: transparent;
        position: absolute;
        z-index: 9997;
        top: 8%;
        right: 12%;
        width: 6%;
        height: 8%;
    }
    .Oval {
        fill: white;
        opacity: 80%;
    }
    .Shape {
        fill: white;
        opacity: 80%;
    }
</style>
