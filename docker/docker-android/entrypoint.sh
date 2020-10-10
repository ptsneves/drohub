#!/usr/bin/env bash
set -eux

if [ -z ${CODE_DIR} ]; then
    >&2 echo "Error: Please specify CODE_DIR as an environmental variable. This is where the drohub code should be available at"
    exit 1
fi

if [ -z ${APP_PATH} ]; then
    >&2 echo "Error: Please specify APP_PATH as an environmental variable. This is where the drohub code should be available at"
    exit 1
fi

if [ -z ${RPC_API_PATH} ]; then
    >&2 echo "Error: Please specify RPC_API_PATH as an environmental variable. This is where the drohub code should be available at"
    exit 1
fi

if [ ! -w /dev/kvm ]; then
    >&2 echo "Error: kvm is not writable"
    exit 1
fi

export REAL_APP_PATH="${CODE_DIR}/${APP_PATH}"
export HOME=${CODE_DIR}/.docker-android-home
export GRADLE_HOME="${HOME}/gradle-home"
export ANDROID_SDK_ROOT=${HOME}
export ANDROID_HOME=${HOME}

mkdir -p ${HOME}
cp -a ${LICENSES_PATH} ${HOME}

cp -a ${REAL_APP_PATH} ${ANDROID_HOME}/
cp -a ${CODE_DIR}/${RPC_API_PATH} ${ANDROID_HOME}/
pushd ${ANDROID_HOME}/${APP_PATH}/android/
rm -rf app/build
./gradlew --gradle-user-home ${GRADLE_HOME} connectedDebugAndroidTest $@