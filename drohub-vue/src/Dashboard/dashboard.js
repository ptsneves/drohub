/* eslint-disable no-unused-vars */
import Vue from 'vue';
import Vuex from 'vuex';
import TelemetryHub from '../plugins/telemetryhub'
import vClickOutside from "v-click-outside";


Vue.config.productionTip = false;
Vue.use(TelemetryHub);
Vue.use(vClickOutside);

Vue.component('battery-level', require('./components/BatteryLevel').default);
Vue.component('radio-signal', require('./components/RadioSignal').default);
Vue.component('zoom-slider', require('./components/ZoomSlider').default);
Vue.component('gimbal-pitch-slider', require('./components/GimbalPitchSlider').default);

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
