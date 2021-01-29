import { shallowMount, createLocalVue, config } from '@vue/test-utils'
import Vuex from 'vuex'
import GalleryTimeLine from "@/Gallery/components/GalleryTimeLine";

import VueWaypoint from 'vue-waypoint'

const localVue = createLocalVue();

localVue.use(Vuex);
localVue.use(VueWaypoint);

describe('GalleryTimeLine.vue', () => {
    let store
    const observeMock = {
        observe: () => null,
        disconnect: () => null // maybe not needed
    };

    const test_data = require('test-data.json').GalleryTimeLine;
    const temp_test_data = require('temporary-test-data.json').GalleryTimeLine;
    const init_data = test_data.special.init_data;
    let propsData = test_data.propsData;
    propsData.antiForgeryToken = temp_test_data.propsData.crossSiteForgeryToken;

    beforeEach(() => {
        const observe = jest.fn();
        const unobserve = jest.fn();


        //For waypoint plugin
        window.IntersectionObserver = jest.fn(() => ({
            observe,
            unobserve,
        }))


    })
    it('Correct interface', () => {
        store = new Vuex.Store({
            state: {
                filtered_tags: [],
                anti_forgery_token: "",
                modal_model: {
                    type: 'INACTIVE',
                },
            },
            mutations: {
                ANTI_FORGERY_TOKEN(state, anti_forgery_token) {
                    state.anti_forgery_token = anti_forgery_token;
                },
            },
        })


        Object.defineProperty(window, "init_data",
            {
                writable: true,
                value: init_data
            }
        );
        const wrapper = shallowMount(GalleryTimeLine, { store, localVue, propsData});
    });
})
