/* eslint-disable no-unused-vars */
import Vue from 'vue';
import Vuex from 'vuex';
import TelemetryHub from '../plugins/telemetryhub'


Vue.config.productionTip = false;
Vue.use(TelemetryHub);
Vue.component('battery-level', require('./components/BatteryLevel').default);
Vue.component('radio-signal', require('./components/RadioSignal').default);

window.Vue = Vue;
window.Vuex = Vuex;
window.onload = function() {
    new Vue({
        el: '#app',
        mounted() {
        },
        created() {
            Vue.prototype.startSignalR();
        }
    });
};
