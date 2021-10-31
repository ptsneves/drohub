import { shallowMount, mount, createLocalVue, config } from '@vue/test-utils'
import Vuex from 'vuex'
import GalleryTimeLine from "@/Gallery/components/GalleryTimeLine";
import vClickOutside from 'v-click-outside'

import VueWaypoint from 'vue-waypoint'


describe('GalleryTimeLine.vue', () => {
    const localVue = createLocalVue();
    const test_data = require('test-data.json').GalleryTimeLine;
    const temp_test_data = require('temporary-test-data.json').GalleryTimeLine;
    const media_list = temp_test_data.mediaIdList;
    const preview_list = temp_test_data.previewMediaIdList;
    const axios = require('axios');
    const crypto = require('crypto');
    const propsData = test_data.propsData;

    const observeMock = {
        observe: () => null,
        disconnect: () => null // maybe not needed
    };

    localVue.use(Vuex);
    localVue.use(VueWaypoint);
    localVue.use(vClickOutside);

    axios.defaults.headers.common['Cookie'] = temp_test_data.cookie;
    axios.defaults.headers.get['Cookie'] = temp_test_data.cookie;

    propsData.antiForgeryToken = temp_test_data.propsData.crossSiteForgeryToken;
    propsData.galleryModelJson = temp_test_data.propsData.galleryModelJson;

    const store = new Vuex.Store({
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

    beforeEach(() => {
        const observe = jest.fn();
        const unobserve = jest.fn();

        //For lightbox
        const pauseStub = jest
            .spyOn(window.HTMLMediaElement.prototype, 'pause')
            .mockImplementation(() => {})


        //For waypoint plugin
        window.IntersectionObserver = jest.fn(() => ({
            observe,
            unobserve,
        }))
    })


    it('Correct interface', () => {
        const wrapper = mount(GalleryTimeLine, { store, localVue, propsData});

        preview_list.forEach(e => expect(wrapper.findAll(`div[file-id='${e.file}']`).exists())
            .toBe(true) );
    });

    it('Reachable img urls and ', () => {
        const wrapper = mount(GalleryTimeLine, { store, localVue, propsData});

        const image_urls = wrapper.findAll('img.gallery-image[src]').wrappers.map(wrapper =>
            wrapper.attributes('src'));

        return Promise.all(image_urls.map(image_url => axios.get(
            new URL(image_url, temp_test_data.siteURI).href, {
                adapter: require('axios/lib/adapters/http'),
                responseType: 'arraybuffer'
            })
            .then((response) => {
                if ( new URL(response.request.path, temp_test_data.siteURI).href !== response.config.url)
                    console.error(`Response redirected us to ${response.request.path} which is not expected`);

                const file_data = preview_list.find(e => image_url.endsWith(e.file))
                expect(
                    crypto.createHash("sha256")
                        .update(response.data)
                        .digest("base64"))
                    .toBe(file_data.sha256);
            })
            .catch( (error) => {
                    console.error(error)
                    if (error.response) {
                        console.error(error.response.headers);
                        reject(`url ${url} did not return success`);
                    }
                }
            )
     ))
    });

    it('Correct lightbox data', () => {
        const wrapper = mount(GalleryTimeLine, {store, localVue, propsData});
        const lightbox_data = wrapper.vm.lightbox_data;
        expect(lightbox_data.length).toBe(3);

        //We use the preview list because there are cases of medias where only preview is available
        expect(lightbox_data).toStrictEqual(
            [
                {
                    "type": "video",
                    "thumb": `/DHub/DeviceRepository/GetPreview?media_id=${preview_list[2].file}`,
                    "sources": [
                        {
                            "src": `/DHub/DeviceRepository/GetLiveStreamRecordingVideo?video_id=${preview_list[2].file}`,
                            "type": "video/webm"
                        }
                    ],
                    "width": 640,
                    "height": 480,
                    "caption": "onboard"
                },
                {
                    "type": "image",
                    "thumb": `/DHub/DeviceRepository/GetPreview?media_id=${preview_list[1].file}`,
                    "src": `/DHub/DeviceRepository/DownloadFile?media_id=${preview_list[1].file}`,
                    "caption": "onboard"
                },
                {
                    "type": "image",
                    "thumb": `/DHub/DeviceRepository/GetPreview?media_id=${preview_list[0].file}`,
                    "src": `/DHub/DeviceRepository/GetPreview?media_id=${preview_list[0].file}`,
                    "caption": "onboard"
                }
            ]
        );
    });

    it('Check lightbox is disabled when selection ongoing', async () => {
        const wrapper = mount(GalleryTimeLine, {store, localVue, propsData});
        wrapper.vm.onGallerySettingsSelection("dummy");
        await wrapper.find('img.gallery-image[src]').trigger('click');
        expect(wrapper.find('img.vib-image[src]').exists()).toBe(false)
    });

    it('Check lightbox is working', async () => {
        const wrapper = mount(GalleryTimeLine, {store, localVue, propsData});
        await wrapper.find('img.gallery-image[src]').trigger('click');
        expect(wrapper.find('img.vib-image[src]').exists()).toBe(true)
    });
})
