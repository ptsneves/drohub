package com.drohub.Fragments;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import androidx.fragment.app.Fragment;
import com.drohub.DroHubHelper;

public class BaseFragment extends Fragment {
    private final int _fragment_id;
    protected View _view;
    protected String _user_auth_token;
    protected String _user_email;

    public BaseFragment(int fragment_id) {
        _fragment_id = fragment_id;
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             Bundle savedInstanceState) {
        if (! (getActivity() instanceof DroHubHelper.CredentialGetters))
            throw new IllegalArgumentException();

        DroHubHelper.CredentialGetters credential_getter =  (DroHubHelper.CredentialGetters)getActivity();
        if (credential_getter == null)
            throw new RuntimeException();

        _user_auth_token = credential_getter.getAuthToken();
        _user_email = credential_getter.getUserEmail();

        _view = inflater.inflate(_fragment_id, container, false);
        return _view;
    }

    protected  <T> T getFragmentViewById(int id) {
        if (_view == null)
            return null;

        return (T)_view.findViewById(id);
    }
}
