<template>
    <div v-if="isActiveModal">
        <div class="modal-dialog" style="z-index: 1000">
            <div class="modal-content modal-content-extra">
                <div class="modal-header">
                    <button type="button" class="close" v-on:click="hideModal">
                        <span aria-hidden="true">&times;</span>
                    </button>
                    <div class="title-container">

                        <img class="img-circle user-img" alt="User photo" v-bind:src="photoSrc">
                        <div>
                            {{userName}}
                        </div>
                    </div>
                </div>
                <form method="post" v-bind:action="postLocation">
                    <div class="modal-body">
                        <div class="modal-body-title">
                            Manage Permissions
                        </div>
                        <div class="option" v-for="(v, user_type, index) in availableUserTypes">
                            <input type="hidden" name="user_email" v-bind:value="userEmail"/>
                            <label style="padding-left: 10px;"  v-bind:for="user_type">
                                <input
                                    type="radio"
                                    name="permission_type"
                                    v-bind:disabled="user_type === userType"
                                    v-bind:id="user_type"
                                    v-bind:value="user_type" v-model="picked" />
                                <span
                                    class="user-type"
                                    v-bind:class="{'user-type-disabled': user_type === userType}"
                                >

                                    {{ user_type }}
                                    <span v-if="user_type === userType">
                                        (Current)
                                    </span>
                                </span>
                                <span class="user-type-description">{{ v.human_description }}</span>
                            </label>
                        </div>
                    </div>
                    <div class="modal-footer modal-footer-extra">
                        <button
                            class="drohub-btn-default drohub-btn-default-text"
                            style="background: grey !important"
                            v-on:click="hideModal"
                        >Close</button>
                        <button
                            type="submit"
                            v-bind:class="{'drohub-btn-default-disabled': !canSubmit}"
                            v-bind:disabled="!canSubmit"
                            class="drohub-btn-default drohub-btn-default-text"
                        >
                        Save Changes</button>
                    </div>
                </form>
            </div>
        </div>
        <div class="modal-overlay"></div>
    </div>
</template>

<script>
    export default {
        name: "AccountChangePermissionsModal",
        data() {
            return {
                MODAL_TYPE: 'CHANGE_PERMISSIONS',
                picked: "",
            }
        },
        props: {
            postLocation: {
                type: String,
                required: true,
            },
            antiForgeryToken: {
                type: String,
                required: true,
            },
            availableUserTypes: {
                type: Object,
                required: true,
            }
        },
        computed: {
            isActiveModal() {
                return this.$store.state.modal_model.type === this.MODAL_TYPE;
            },
            photoSrc() {
                return this.$store.state.modal_model.photo_src;
            },
            userName() {
                return this.$store.state.modal_model.user_name;
            },
            userEmail() {
                return this.$store.state.modal_model.user_email;
            },
            userType() {
                return this.$store.state.modal_model.user_type;
            },
            canSubmit() {
                return this.picked !== '';
            }
        },
        methods: {
            hideModal() {
                return this.$store.commit('SET_MODAL_MODEL', {type:'INACTIVE'});
            },
        }
    }
</script>

<style scoped>
    @import '../../assets/modal.css';
    @import '../../assets/button.css';

.user-img {
    max-width: 68px;
    max-height: 68px;
}

.title-container {
    text-align: center;
    font-size: 24px;
    margin-bottom: 40px;
    margin-top: 40px;
}

.user-type {
    text-transform: capitalize;
    font-size: 20px;
    margin-left: 10px;
    margin-right: 10px;
}

.user-type-disabled {
    color: grey !important;
}

.user-type-description {
    color: grey;
    font-size: 16px;
    display: flex;
    margin-left: 25px;
    margin-right: 20px;
}

.option {
    display: flex;
    margin-top: 12px;
    margin-bottom: 12px;
}

.modal-body-title {
    font-size: 27px;
    color: grey;
}

</style>
