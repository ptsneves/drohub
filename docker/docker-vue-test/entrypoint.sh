#!/usr/bin/env bash
set -eux


export HOME=/.yarn/
cp -r wwwroot drohub-vue /tmp/destdir/
cd /tmp/destdir/drohub-vue
yarn install
yarn build --mode=development
yarn test:unit $@