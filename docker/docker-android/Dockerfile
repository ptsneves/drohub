FROM ubuntu:focal

USER root

ENV ANDROID_HOME=/opt/android-sdk-linux \
    LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8 \
    LANGUAGE=en_US:en

ENV ANDROID_SDK_ROOT=$ANDROID_HOME \
    PATH=${PATH}:${ANDROID_HOME}/cmdline-tools/tools/bin:${ANDROID_HOME}/platform-tools:${ANDROID_HOME}/emulator

ENV ANDROID_SDK_TOOLS_VERSION=6609375 \
    BUILDTOOLS_VERSION=29.0.3 \
    PLATFORM_VERSION=30

RUN set -o xtrace \
    && cd /opt \
    && apt-get update \
    && apt-get install -y openjdk-8-jdk wget unzip zip \
    && rm -rf /var/lib/apt/lists/* \
    &&  wget -q https://dl.google.com/android/repository/commandlinetools-linux-${ANDROID_SDK_TOOLS_VERSION}_latest.zip -O android-sdk-tools.zip \
    && mkdir -p ${ANDROID_HOME}/cmdline-tools/ \
    && unzip -q android-sdk-tools.zip -d ${ANDROID_HOME}/cmdline-tools/ \
    && ln -sf ${ANDROID_HOME}/cmdline-tools/tools ${ANDROID_HOME}/cmdline-tools/latest \
    && chown -R root:root $ANDROID_HOME \
    && rm android-sdk-tools.zip \
    && echo '%sudo ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers \
    && yes | sdkmanager --licenses \
    && touch /root/.android/repositories.cfg \
    && sdkmanager platform-tools emulator "platforms;android-${PLATFORM_VERSION}" "build-tools;${BUILDTOOLS_VERSION}" "system-images;android-${PLATFORM_VERSION};google_apis;x86_64" \
    && mkdir -p /root/.android \
    && touch /root/.android/repositories.cfg 

ENTRYPOINT [ "./gradlew" ]
CMD ["--gradle-user-home", "docker-gradle-home/",  "connectedAndroidTest"]

