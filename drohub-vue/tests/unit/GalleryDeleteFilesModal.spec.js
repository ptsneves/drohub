import { shallowMount, mount, createLocalVue, config } from '@vue/test-utils';
import Vuex from 'vuex';
import GalleryDeleteFilesModal from "@/Gallery/components/GalleryDeleteFilesModal";

const localVue = createLocalVue();

localVue.use(Vuex);
const test_data = require('test-data.json').GalleryDeleteFilesModal;
const temp_test_data = require('temporary-test-data.json').GalleryDeleteFilesModal;
let propsData = test_data.propsData;
propsData.antiForgeryToken = temp_test_data.propsData.crossSiteForgeryToken;

describe('GalleryDeleteFilesModal.vue', () => {
    it('Correct interface', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: [],
                modal_model: {
                    type: 'INACTIVE',
                },
            },
        })
        shallowMount(GalleryDeleteFilesModal, { store, localVue, propsData});
    });

    it('modal model active', () => {
        const store = new Vuex.Store({
            state: {
                filtered_tags: [],
                modal_model: {
                    type: 'DELETE_FILES',
                },
            },
        });
        const wrapper = shallowMount(GalleryDeleteFilesModal, { store, localVue, propsData});
        expect(wrapper.find("div.gallery-delete-files-modal").exists()).toBe(true);
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
        const wrapper = shallowMount(GalleryDeleteFilesModal, { store, localVue, propsData});
        expect(wrapper.find("div.gallery-add-tag-modal").exists()).toBe(false);
    });

    it('remove in device successful', async () => {
        const store = new Vuex.Store({
            state: {
                modal_model: {
                    type: 'DELETE_FILES',
                    MediaIdList: [ temp_test_data.mediaIdList[0] ]
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

        const wrapper = mount(GalleryDeleteFilesModal, { store, localVue, propsData});
        const checkbox = wrapper.find('input#checkbox_remove_in_device');
        await checkbox.setChecked();

        wrapper.vm.submit();
        return promise
            .then((r) => {
                expect(r.request.path).toEqual(test_data.special.expectedResultPage);
            })
            .catch((e) => console.error(e));
    });

    it('remove in drohub successful', async () => {
        const store = new Vuex.Store({
            state: {
                modal_model: {
                    type: 'DELETE_FILES',
                    MediaIdList: [ temp_test_data.mediaIdList[1] ]
                },
            },
        });

        expect(temp_test_data.mediaIdList.length).toBe(2);
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
                    .then((response) => {
                        resolve(response);
                    })
                    .catch((e) => reject(e));
            });
        });

        const wrapper = mount(GalleryDeleteFilesModal, { store, localVue, propsData});
        const checkbox = wrapper.find('input#checkbox_remove_in_drohub');
        await checkbox.setChecked();

        wrapper.vm.submit();
        return promise
            .then((r) => {
                expect(r.request.path).toEqual(test_data.special.expectedResultPage);
            })
            .catch((e) => console.error(e));
    });
})
