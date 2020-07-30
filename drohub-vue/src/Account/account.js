/* eslint-disable no-unused-vars */
import Vue from 'vue';
import Vuex from 'vuex';
import vClickOutside from 'v-click-outside'
import VueWaypoint from 'vue-waypoint'

Vue.component('account-send-invitation', require('./components/AccountSendInvitation').default);
Vue.component('account-user-permissions', require('./components/AccountUserPermissions').default);
Vue.component('account-change-permissions-modal', require('./components/AccountChangePermissionsModal').default);
Vue.component('account-exclude-user-modal', require('./components/AccountExcludeUserModal').default);

Vue.config.productionTip = false;
Vue.use(Vuex);
Vue.use(vClickOutside)
Vue.use(VueWaypoint)

const store = new Vuex.Store({
    state: {
        anti_forgery_token: "",
        modal_model: {
            type: 'INACTIVE',
        },
    },
    mutations: {
        SET_MODAL_MODEL(state, payload) {
            if (['CHANGE_PERMISSIONS', 'EXCLUDE_USER'].indexOf(payload.type) > -1) {
                if(state.modal_model.type !== 'INACTIVE') {
                    console.error(`Another ${state.modal_model.type} modal is already active`);
                    return;
                }
                state.modal_model = payload;
            }
            else if (payload.type === 'INACTIVE') {
                state.modal_model = {
                    type: 'INACTIVE',
                };
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
        }
    });
};
