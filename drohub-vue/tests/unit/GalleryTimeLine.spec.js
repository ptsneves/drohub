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
    const init_data = test_data.special.init_data;
    const propsData = test_data.propsData;

    beforeEach(() => {
        const observe = jest.fn();
        const unobserve = jest.fn();


        //For waypoint plugin
        window.IntersectionObserver = jest.fn(() => ({
            observe,
            unobserve,
        }))

        store = new Vuex.Store({
            state: {
                filtered_tags: [],
                anti_forgery_token: "",
                modal_model: {
                    type: 'INACTIVE',
                },
            },
        })


        Object.defineProperty(window, "init_data",
            {
                writable: true,
                value: init_data
            }
        );
    })
    it('Correct interface', () => {

        const wrapper = shallowMount(GalleryTimeLine, { store, localVue, propsData});
    });
})
