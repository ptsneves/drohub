/* eslint-disable no-unused-vars */
import Vue from 'vue';
import Vuex from 'vuex';
import vClickOutside from 'v-click-outside'
import VueWaypoint from 'vue-waypoint'

Vue.config.productionTip = false;
Vue.component('gallery-input', require('./components/GalleryInput.vue').default);
Vue.component('gallery-timeline', require('./components/GalleryTimeLine.vue').default);
Vue.component('media-tag-label', require('./components/VueMediaTagLabel.vue').default)

Vue.use(Vuex);
Vue.use(vClickOutside)
Vue.use(VueWaypoint)

const store = new Vuex.Store({
    state: {
        filtered_tags: [],
        anti_forgery_token: "",
        modal_model: {
            type: 'INACTIVE',
        },
    },
    mutations: {
        ADD_TRACKED_TAG(state, tag) {
            if (state.filtered_tags.indexOf(tag) === -1)
                state.filtered_tags.push(tag);
        },
        REMOVE_TRACKED_TAG(state, tag) {
            const index = state.filtered_tags.indexOf(tag);
            if (index > -1)
                state.filtered_tags.splice(index, 1);
        },
        ANTI_FORGERY_TOKEN(state, anti_forgery_token) {
            state.anti_forgery_token = anti_forgery_token;
        },
        SET_MODAL_MODEL(state, payload) {
            if (['ADD_TAGS', 'DOWNLOAD', 'DELETE_FILES', 'INACTIVE'].indexOf(payload.type) > -1) {
                state.modal_model = payload;
            }
            else
                console.error("Invalid modal selection type")
        }
    },
});

window.Vue = Vue;
window.Vuex = Vuex;
window.onload = function() {
    new Vue({
        el: '#app',
        store,
        mounted() {
            store.commit('ANTI_FORGERY_TOKEN', window.anti_forgery_token);
        }
    });
};
