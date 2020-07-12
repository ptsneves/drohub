<template>
    <div>
        <vue-tags-input
            v-model="tag"
            v-bind:tags="getTags"
            v-bind:add-on-key="[13, ',', ';']"
            v-on:tags-changed="trackTagsChanged"
        />
    </div>
</template>

<script>
import VueTagsInput from '@johmun/vue-tags-input';

export default {
    name: 'GalleryInput',
    components: {
        VueTagsInput,
    },
    computed: {
      getTags() {
          this.tags =this.$store.state.filtered_tags;
          return this.$store.state.filtered_tags.map(t => ({ "text": t}))
      }
    },
    methods: {
        trackTagsChanged(raw_tag_array) {
            let tag_array = raw_tag_array.map(t => t.text);
            let diff = [];
            if (tag_array.length > this.tags.length) {
                diff = tag_array.filter(t => !this.tags.includes(t));
                this.$store.commit('ADD_TRACKED_TAG', diff[0])
            }
            else {
                diff = this.tags.filter(t => !tag_array.includes(t));
                this.$store.commit('REMOVE_TRACKED_TAG', diff[0])
            }
            this.tags = tag_array;
        }
    },
    data() {
        return {
            tag: '',
            tags: [],
        };
    },
};
</script>

<style lang="css">
    /* style the background and the text color of the input ... */
    .vue-tags-input {
        background: #000000;
    }

    /* some stylings for the autocomplete layer */
    .vue-tags-input .ti-autocomplete {
        background: #283944;
        border: 1px solid #8b9396;
        border-top: none;
    }

    /* the selected item in the autocomplete layer, should be highlighted */
    .vue-tags-input .ti-item.ti-selected-item {
        background: #ebde6e;
        color: #283944;
    }

    /* default styles for all the tags */
    .vue-tags-input .ti-tag {
        width: auto;
        border-radius: 20px;
        padding-inline: 22px;
        margin: 1px;
        color:white;
        background: #758093;
        display: flex;
    }

    /* the styles if a tag is invalid */
    .vue-tags-input .ti-tag.ti-invalid {
        background-color: #e88a74;
    }

    /* if the user input is invalid, the input color should be red */
    .vue-tags-input .ti-new-tag-input.ti-invalid {
        color: #e88a74;
    }

    /* if a tag or the user input is a duplicate, it should be crossed out */
    .vue-tags-input .ti-duplicate span,
    .vue-tags-input .ti-new-tag-input.ti-duplicate {
        text-decoration: line-through;
    }

    .vue-tags-input .ti-deletion-mark:after {
        transform: scaleX(1);
    }
</style>
