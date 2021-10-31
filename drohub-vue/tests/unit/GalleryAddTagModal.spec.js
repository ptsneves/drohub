import { shallowMount, mount, createLocalVue, config } from '@vue/test-utils';
import Vuex from 'vuex';
import GalleryAddTagModal from "@/Gallery/components/GalleryAddTagModal";

const localVue = createLocalVue();

localVue.use(Vuex);
const test_data = require('test-data.json').GalleryAddTagModal;
const temp_test_data = require('temporary-test-data.json').GalleryAddTagModal;
let propsData = test_data.propsData;
propsData.antiForgeryToken = temp_test_data.propsData.crossSiteForgeryToken;
propsData.addTagsPostUrl = new URL(propsData.addTagsPostUrl, temp_test_data.siteURI).href

describe('GalleryAddTagModal.vue', () => {
    it('Correct interface', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: [],
                modal_model: {
                    type: 'INACTIVE',
                },
            },
        })
        shallowMount(GalleryAddTagModal, { store, localVue, propsData});
    });

    it('modal model active', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: [],
                modal_model: {
                    type: 'ADD_TAGS',
                },
            },
        });
        const wrapper = mount(GalleryAddTagModal, { store, localVue, propsData});
        expect(wrapper.find("div.gallery-add-tag-modal").exists()).toBe(true);
    });

    it('modal model inactive', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: [],
                modal_model: {
                    type: 'INACTIVE',
                },
            },
        });
        const wrapper = mount(GalleryAddTagModal, { store, localVue, propsData});
        expect(wrapper.find("div.gallery-add-tag-modal").exists()).toBe(false);
    });

    it('submit with no tags selected or inactive', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: [],
                modal_model: {
                    type: 'INACTIVE',
                },
            },
        });
        const axios = require('axios');
        const original_post = axios.post;
        axios.defaults.adapter = require('axios/lib/adapters/http');
        axios.post = jest.fn(() => {
            console.error("Should never be called");
            return Promise.reject("Should never be called");
        });
        const wrapper = mount(GalleryAddTagModal, { store, localVue, propsData});

        wrapper.vm.submit();
        axios.post = original_post;
    });

    it('submit with tags selected', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: ['hello'],
                modal_model: {
                    type: 'ADD_TAGS',
                    TimeStampInSeconds: 0,
                    UseTimeStamp: temp_test_data.useTimeStamp,
                    MediaIdListJSON: temp_test_data.mediaIdList
                },
            },
        });

        delete window.location
        window.location = {
            assign: jest.fn(),
            reload: jest.fn()
        }

        const axios = require('axios');
        const real_axios_post = axios.post;
        axios.defaults.headers.post['Cookie'] = temp_test_data.cookie;

        const promise = new Promise((resolve, reject) => {
            axios.post = jest.fn((url, query_string) => {
                axios.post = real_axios_post;
                //Require the adapter otherwise jsdom intercepts and fucks the headers removing
                // 4 example the cookies
                // https://github.com/axios/axios/issues/1180#issuecomment-373268257
                return real_axios_post(url, query_string, {
                        adapter: require('axios/lib/adapters/http')
                    })
                    .then((r) => {
                        resolve(r)
                    })
                    .catch((e) => reject(e));
            });
        });

        const wrapper = shallowMount(GalleryAddTagModal, { store, localVue, propsData});
        wrapper.setData({
            tags: temp_test_data.tagsToAdd.map(t => {
                return {
                    text: t
                };
            })
        });
        wrapper.vm.submit();
        return promise
            .then((r) => {
                expect(r.request.path).toEqual(test_data.special.expectedResultPage);
            })
            .catch((e) => console.error(e));
    });
})
