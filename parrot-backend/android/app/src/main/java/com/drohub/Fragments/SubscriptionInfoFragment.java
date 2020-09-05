package com.drohub.Fragments;

import android.content.Context;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;
import com.drohub.DroHubHelper;
import com.drohub.IInfoDisplay;
import com.drohub.R;
import com.drohub.SnackBarInfoDisplay;
import com.drohub.api.GetSubscriptionInfoHelper;
import org.json.JSONObject;

import java.net.URISyntaxException;


public class SubscriptionInfoFragment extends BaseFragment {
    public SubscriptionInfoFragment() {
        super(R.layout.fragment_subscription_info);
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             Bundle savedInstanceState) {

        super.onCreateView(inflater, container, savedInstanceState);

        final View root_view = getActivity().getWindow().getDecorView().findViewById(android.R.id.content);
        final IInfoDisplay error_display = new SnackBarInfoDisplay(root_view, 5000);

        String sub_info_url;
        try {
            Context context = getContext();
            if (context == null)
                throw new RuntimeException();
            sub_info_url = DroHubHelper.getURL(context, R.string.get_subscription_info_url);
        } catch (URISyntaxException e) {
            throw new RuntimeException();
        }

        final GetSubscriptionInfoHelper sub_info_helper = new GetSubscriptionInfoHelper(
                error_display,
                this::updateData,
                _user_email,
                _user_auth_token,
                sub_info_url
        );
        _view.setOnClickListener(v -> sub_info_helper.get());
        sub_info_helper.get();
        return _view;
    }

    public void updateData(JSONObject response) {
        JSONObject result = response.optJSONObject("result");
        if (result == null)
            return;

        String sub_name = result.optString("subscription_name");
        if (sub_name.isEmpty())
            return;

        JSONObject allowed_flight_time_obj = result.optJSONObject("allowed_flight_time");
        if(allowed_flight_time_obj == null)
            return;
        double allowed_flight_time = allowed_flight_time_obj.optDouble("totalMinutes");


        int allowed_users = result.optInt("allowed_users");

        TextView subinfo_text_view = getFragmentViewById(R.id.flight_area_name);
        subinfo_text_view.setText(sub_name);

        TextView user_id_text_view = getFragmentViewById(R.id.user_id);
        user_id_text_view.setText(_user_email);

        TextView allowed_flight_time_text_view = getFragmentViewById(R.id.allowed_flight_time);
        allowed_flight_time_text_view.setText(String.format("%d minutes", (int)allowed_flight_time));

        TextView allowed_user_text_view = getFragmentViewById(R.id.allowed_users);
        allowed_user_text_view.setText(String.format("%d", allowed_users));
    }
}
